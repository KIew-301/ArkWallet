using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Orchestrators;

#pragma warning disable S107 // DI-контейнер: число зависимостей оркестратора оправдано
internal class MarketMakerOrchestrator(
    ArkWalletDbContext dbContext,
    IMarketMakerBotRegistrationService botRegistrationService,
    IPortfolioUpdatingService portfolioUpdatingService,
    IOrderCreationService orderCreationService,
    IMarketMakerOrderService marketMakerOrderService,
    MarketMakerGridEngine marketMakerGridEngine,
    ILogger<MarketMakerOrchestrator> logger,
    TimeProvider? timeProvider = null) : IMarketMakerOrchestrator
{
#pragma warning restore S107
    private static readonly long[] TraderIds = [101L, 102L];
    public async Task<Result> EnsureBotsRegisteredAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync();

            foreach (var symbol in tokens)
            {
                foreach (var (traderId, role) in new[] { (101L, BotRole.Buyer), (102L, BotRole.Seller) })
                {
                    var exists = await dbContext.MarketMakerBots
                        .AnyAsync(b => b.TraderId == traderId && b.Symbol == symbol);

                    if (exists)
                        continue;

                    var botResult = await botRegistrationService.RegisterBotAsync(
                        (int)traderId,
                        symbol,
                        role,
                        20m);

                    if (!botResult.IsSuccess)
                    {
                        logger.LogError("Failed to register bot {TraderId} for {Symbol}: {Error}", traderId, symbol, botResult.Message);
                        return Result.Fail($"Не удалось зарегистрировать бота {traderId} для {symbol}: {botResult.Message}");
                    }

                    logger.LogInformation("Bot {TraderId} registered with role {Role} for {Symbol}", traderId, role, symbol);
                }
            }

            await CleanupBotsAsync();

            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    private async Task CleanupBotsAsync()
    {
        var activeSymbols = await dbContext.CharacterTokens
            .Where(t => t.IsActive)
            .Select(t => t.Symbol)
            .ToListAsync();

        await dbContext.MarketMakerBots
            .Where(b => !activeSymbols.Contains(b.Symbol))
            .ExecuteDeleteAsync();

        var bots = await dbContext.MarketMakerBots
            .Select(b => new { b.Id, b.TraderId, b.Symbol, b.Role })
            .ToListAsync();

        var duplicateIds = bots
            .GroupBy(b => new { b.TraderId, b.Symbol, b.Role })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Where(b => b.Id != g.Min(x => x.Id)).Select(b => b.Id))
            .ToList();

        if (duplicateIds.Count > 0)
        {
            await dbContext.MarketMakerBots
                .Where(b => duplicateIds.Contains(b.Id))
                .ExecuteDeleteAsync();
        }
    }

    public async Task<Result> EnsureTraderBalancesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            await EnsureTraderBalancesCoreAsync();

            var tokens = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync();

            foreach (var symbol in tokens)
            {
                foreach (var traderId in TraderIds)
                {
                    var result = await UpdateTraderPortfolioAsync(traderId, symbol);
                    if (!result.IsSuccess)
                        return result;
                }
            }

            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    public async Task<Result> UpdateAllBotsGridAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await dbContext.MarketMakerBots
                .Where(b => b.IsActive)
                .ToListAsync();

            if (bots.Count == 0)
                return Result.Fail("Список ботов пуст");

            var tokens = await LoadTokensForBotsAsync(bots);
            await LoadActiveOrdersForBotsAsync(bots);

            foreach (var bot in bots)
            {
                var result = await UpdateBotGridAsync(bot, tokens.GetValueOrDefault(bot.Symbol));
                if (!result.IsSuccess)
                    return result;
            }

            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    public async Task<Result> ProcessBotsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await dbContext.MarketMakerBots
                .Where(b => b.IsActive)
                .ToListAsync();

            bots = bots.OrderBy(_ => Guid.NewGuid()).ToList();

            if (bots.Count == 0)
                return Result.Fail("Список ботов пуст");

            var tokens = await LoadTokensForBotsAsync(bots);
            await LoadActiveOrdersForBotsAsync(bots);

            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

            foreach (var bot in bots)
            {
                if (now >= bot.NextPowerChange)
                {
                    bot.UpdatePower(10, 50, timeProvider);
                    logger.LogDebug("Bot {BotId} power updated to {Power}", bot.Id, bot.BasePower);
                }

                if (now >= bot.NextRebalance || now.Minute == 0)
                {
                    var result = await UpdateBotGridAsync(bot, tokens.GetValueOrDefault(bot.Symbol));
                    if (!result.IsSuccess)
                        return result;

                    bot.UpdateRebalanced(timeProvider);
                    logger.LogDebug("Bot {BotId} grid updated", bot.Id);
                }
            }

            var marketOrderResult = await marketMakerOrderService.ExecuteMarketMakerOrdersAsync(bots.Select(b => b.Id));
            if (!marketOrderResult.IsSuccess)
            {
                logger.LogWarning("Failed to execute market orders: {Error}", marketOrderResult.Message);
            }

            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }

    private async Task<Dictionary<string, CharacterToken>> LoadTokensForBotsAsync(List<MarketMakerBot> bots)
    {
        var symbols = bots.Select(b => b.Symbol).Distinct().ToArray();
        return await dbContext.CharacterTokens
            .Where(t => symbols.Contains(t.Symbol))
            .ToDictionaryAsync(t => t.Symbol);
    }

    private async Task LoadActiveOrdersForBotsAsync(List<MarketMakerBot> bots)
    {
        var symbols = bots.Select(b => b.Symbol).Distinct().ToArray();
        await dbContext.TradeOrders
            .Where(o => symbols.Contains(o.CharacterTokenId) && o.Status == OrderStatus.Active)
            .ToListAsync();
    }

    private async Task<Result> UpdateTraderPortfolioAsync(long traderId, string symbol)
    {
        var portfolioResult = await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
            traderId, symbol, 100_000_000);

        if (!portfolioResult.IsSuccess)
        {
            logger.LogError("Failed to update portfolio for trader {TraderId} on {Symbol}: {Error}", traderId, symbol, portfolioResult.Message);
            return Result.Fail($"Не удалось обновить портфель трейдера {traderId} для {symbol}: {portfolioResult.Message}");
        }

        return Result.Ok();
    }

    private async Task EnsureTraderBalancesCoreAsync()
    {
        await TransactionHandler.ExecuteAsync(dbContext, async () =>
        {
            await dbContext.LockTradersAsync(TraderIds);

            foreach (var traderId in TraderIds)
            {
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                if (trader == null)
                {
                    logger.LogWarning("Trader {TraderId} not found", traderId);
                    continue;
                }

                if (trader.Balance < 1_000_000_000m)
                {
                    trader.AddToBalance(1_000_000_000m - trader.Balance);
                    trader.MarkDirty();
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Trader {TraderId} balance replenished", traderId);
                }
            }

            return Result.Ok();
        });
    }

    private async Task<Result> UpdateBotGridAsync(MarketMakerBot bot, CharacterToken? token)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (token == null)
            {
                logger.LogWarning("Token {Symbol} not found", bot.Symbol);
                return Result.Fail($"Токен {bot.Symbol} не найден");
            }

            var existingOrders = dbContext.TradeOrders.Local
                .Where(o => o.CharacterTokenId == bot.Symbol
                            && o.TraderTelegramId == bot.TraderId
                            && o.Status == OrderStatus.Active)
                .ToList();

            var commands = marketMakerGridEngine.GetOrdersToPlace(bot, token.CurrentPrice, existingOrders);

            if (commands.Count == 0)
            {
                logger.LogDebug("No orders to place for bot {BotId}", bot.Id);
                return Result.Ok();
            }

            var result = await orderCreationService.CreateOrdersAsync(commands);

            if (!result.IsSuccess)
                logger.LogWarning("Failed to create order for bot {BotId}: {Error}", bot.Id, result.Message);

            logger.LogDebug("Grid updated for bot {BotId}, {Count} orders placed", bot.Id, commands.Count);
            return Result.Ok();
        }, logger, nameof(MarketMakerOrchestrator));
    }
}