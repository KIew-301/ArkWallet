using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Orchestrators;

#pragma warning disable S107 // DI-контейнер: число зависимостей оркестратора оправдано
internal class MarketWallBlockerOrchestrator(
    ArkWalletDbContext dbContext,
    ITraderRegistrationService traderRegistrationService,
    IPortfolioUpdatingService portfolioUpdatingService,
    IOrderCancellationService orderCancellationService,
    IOrderCreationService orderCreationService,
    WallBlockerEngine wallBlockerEngine,
    ILogger<MarketWallBlockerOrchestrator> logger,
    TimeProvider? timeProvider = null) : IMarketWallBlockerOrchestrator
{
#pragma warning restore S107
    private const long WallBlockerTraderId = 103;
    private const string NextExecutionKey = "WallBlockerNextExecution";
    private const decimal TargetBalance = 1_000_000_000m;
    private const int TargetTokens = 100_000_000;

    public async Task<Result> EnsureRegisteredAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var exists = await dbContext.Traders.AnyAsync(t => t.TelegramId == WallBlockerTraderId);

            if (exists)
                return Result.Ok();

            var result = await traderRegistrationService.RegisterTraderAsync(WallBlockerTraderId, "WallBlocker", false);

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to register WallBlocker trader: {Error}", result.Message);
                return Result.Fail($"Не удалось зарегистрировать трейдера 103: {result.Message}");
            }

            logger.LogInformation("WallBlocker trader {TraderId} registered", WallBlockerTraderId);
            return Result.Ok();
        }, logger, nameof(MarketWallBlockerOrchestrator));
    }

    public async Task<Result> EnsureBalancesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([WallBlockerTraderId]);

                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == WallBlockerTraderId);
                if (trader == null)
                {
                    logger.LogWarning("Trader {TraderId} not found", WallBlockerTraderId);
                    return Result.Fail("Трейдер не найден");
                }

                if (trader.Balance < TargetBalance)
                {
                    trader.AddToBalance(TargetBalance - trader.Balance);
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("WallBlocker trader {TraderId} balance replenished", WallBlockerTraderId);
                }

                return Result.Ok();
            });

            var symbols = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync();

            foreach (var symbol in symbols)
            {
                var portfolioResult = await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
                    WallBlockerTraderId, symbol, TargetTokens);

                if (!portfolioResult.IsSuccess)
                {
                    logger.LogError("Failed to update portfolio for trader {TraderId} on {Symbol}: {Error}", WallBlockerTraderId, symbol, portfolioResult.Message);
                    return Result.Fail($"Не удалось обновить портфель трейдера {WallBlockerTraderId} для {symbol}: {portfolioResult.Message}");
                }
            }

            return Result.Ok();
        }, logger, nameof(MarketWallBlockerOrchestrator));
    }

    public async Task<Result> ExecuteIterationAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

            var state = await dbContext.AppStates.FindAsync(NextExecutionKey);
            if (state is { } existing && existing.GetValue<DateTime>() is { } nextExecution && now < nextExecution)
                return Result.Ok();

            var tokens = await LoadActiveTokensAsync();

            if (tokens.Count == 0)
                return Result.Ok();

            var avgPowerBySymbol = await LoadAveragePowerBySymbolAsync(tokens.Select(t => t.Symbol).ToArray());

            await CancelExistingOrdersAsync();

            var commands = BuildWallOrders(tokens, avgPowerBySymbol);

            if (commands.Count > 0)
            {
                var createResult = await orderCreationService.CreateOrdersAsync(commands);
                if (!createResult.IsSuccess)
                    return Result.Fail($"Не удалось создать ордера: {createResult.Message}");
            }

            SaveNextExecution(state, now);
            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }, logger, nameof(MarketWallBlockerOrchestrator));
    }

    private sealed record ActiveToken(string Symbol, decimal CurrentPrice);

    private async Task<List<ActiveToken>> LoadActiveTokensAsync()
    {
        return (await dbContext.CharacterTokens
            .Where(t => t.IsActive)
            .Select(t => new { t.Symbol, t.CurrentPrice })
            .ToListAsync())
            .Select(t => new ActiveToken(t.Symbol, t.CurrentPrice))
            .ToList();
    }

    private async Task<Dictionary<string, decimal>> LoadAveragePowerBySymbolAsync(string[] symbols)
    {
        return await dbContext.MarketMakerBots
            .Where(b => b.IsActive && symbols.Contains(b.Symbol))
            .GroupBy(b => b.Symbol)
            .Select(g => new { Symbol = g.Key, AvgPower = g.Average(b => b.BasePower) })
            .ToDictionaryAsync(g => g.Symbol, g => g.AvgPower);
    }

    private async Task CancelExistingOrdersAsync()
    {
        var cancelResult = await orderCancellationService.CancelAllOrderAsync(WallBlockerTraderId);
        if (!cancelResult.IsSuccess && !string.Equals(cancelResult.Message, "Нет активных ордеров для отмены", StringComparison.Ordinal))
        {
            logger.LogWarning("Failed to cancel WallBlocker orders: {Error}", cancelResult.Message);
        }
    }

    private List<CreateOrderCommand> BuildWallOrders(List<ActiveToken> tokens, Dictionary<string, decimal> avgPowerBySymbol)
    {
        var commands = new List<CreateOrderCommand>();

        foreach (var token in tokens)
        {
            var avgPower = avgPowerBySymbol.TryGetValue(token.Symbol, out var power) && power > 0 ? power : 0m;

            foreach (var level in wallBlockerEngine.GetLevels(token.CurrentPrice))
            {
                var spread = Random.Shared.Next(0, 41);
                var quantity = (int)Math.Max(avgPower * Random.Shared.Next(20, 101) * (1 + spread / 100m), 1);

                commands.Add(new CreateOrderCommand(
                    WallBlockerTraderId,
                    level.Direction,
                    token.Symbol,
                    quantity,
                    Math.Round(level.Price, 2)));
            }
        }

        return commands;
    }

    private void SaveNextExecution(AppState? state, DateTime now)
    {
        var nextExecutionTime = now.AddMinutes(Random.Shared.Next(45, 141));
        if (state == null)
            dbContext.AppStates.Add(AppState.Create(NextExecutionKey, nextExecutionTime));
        else
            state.UpdateValue(nextExecutionTime);
    }
}
