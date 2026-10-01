using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.TradingContext.Domain.Events;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.General.Application.Services.Orchestrators;

#pragma warning disable S107 // DI-контейнер: число зависимостей оркестратора оправдано

internal class BotOrchestrator(
    ArkWalletDbContext dbContext,
    IPlanModifierCollection modifierCollection,
    IMarketMakerBotRegistrationService botRegistration,
    IOrderCollector orderCollector,
    IOrderCreationService orderCreationService,
    IUpdatingService portfolioUpdatingService,
    IOrderCancellationService orderCancellationService,
    IEventPublisher eventPublisher,
    ILogger<BotOrchestrator> logger) : IBotOrchestrator
{
    private readonly MarketDataProvider _marketDataProvider = new(dbContext);
#pragma warning restore S107

    public async Task<Result> UpdateAllBotsBalancesAsync(CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await LoadActiveBotsAsync(ct);
            if (bots.Count == 0)
                return Result.Ok();

            var activeTokenSymbols = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync(ct);

            await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                foreach (var bot in bots)
                {
                    var (balance, portfolioTokens) = MarketMakerBot.GetDefaultResources();

                    var processed = await ProcessBotTraderAsync(bot, balance, ct);
                    if (!processed)
                        continue;

                    await RefreshPortfolioBatchAsync(bot, activeTokenSymbols, portfolioTokens);
                }

                return Result.Ok();
            });

            return Result.Ok();
        }, logger, nameof(BotOrchestrator));
    }

    private async Task<bool> ProcessBotTraderAsync(
        MarketMakerBotRecord bot, decimal requiredBalance, CancellationToken ct)
    {
        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == bot.TraderId, ct);
        if (trader == null)
        {
            logger.LogWarning("Trader {TraderId} not found for bot {BotId}", bot.TraderId, bot.Id);
            return false;
        }

        if (trader.Balance < requiredBalance)
        {
            trader.Balance = requiredBalance;
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Trader {TraderId} balance replenished to {Balance}", trader.Id, requiredBalance);
        }

        return true;
    }

    private async Task RefreshPortfolioBatchAsync(
        MarketMakerBotRecord bot, IReadOnlyList<string> activeTokenSymbols, int portfolioTokens)
    {
        var symbols = GetSymbolList(bot, activeTokenSymbols);
        foreach (var symbol in symbols)
        {
            var portfolioResult = await portfolioUpdatingService
                .CreateOrUpdatePortfolioAsync(bot.TraderId, symbol, portfolioTokens);
            if (!portfolioResult.IsSuccess)
                logger.LogWarning(
                    "Failed to update portfolio for trader {TraderId} on {Symbol}: {Error}",
                    bot.TraderId, symbol, portfolioResult.Message);
        }
    }

    private static IReadOnlyList<string> GetSymbolList(
        MarketMakerBotRecord bot, IReadOnlyList<string> activeTokenSymbols)
        => bot.Symbol == "*"
            ? activeTokenSymbols
            : (IReadOnlyList<string>)new List<string> { bot.Symbol };

    public async Task<Result> UpdateBotsGridsAsync(CancellationToken ct = default)
    {
        var buyerResult = await UpdateBotsGridsForRoleAsync(MarketMakerRole.Buyer, ct);
        if (!buyerResult.IsSuccess)
            return buyerResult;

        return await UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller, ct);
    }

    public async Task<Result> UpdateBotsGridsForRoleAsync(MarketMakerRole role, CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await LoadActiveBotsAsync(ct, role);
            if (bots.Count == 0)
                return Result.Ok();

            var isWall = role == MarketMakerRole.Waller;
            var modifiers = isWall ? modifierCollection.WallGridModifiers : modifierCollection.GridModifiers;

            var mask = AggregateRequiredData(modifiers);
            var snapshots = await LoadSnapshotsAsync(mask, bots, ct);

            foreach (var bot in Shuffle(bots))
            {
                if (isWall)
                    await CancelWallOrdersAsync(bot.TraderId);

                var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
                var plan = BuildPlan(domainBot, modifiers, mask, snapshots, bot.Symbol);
                if (plan == null)
                    continue;

                orderCollector.Add(MarketMakerGridMapper.ToCommands(plan));
                await eventPublisher.PublishAsync(new BotPublicOrdersEvent(plan), ct);
            }

            return await PlaceCollectedAsync();
        }, logger, nameof(BotOrchestrator));
    }

    private async Task CancelWallOrdersAsync(long traderId)
    {
        var cancelResult = await orderCancellationService.CancelAllOrderAsync(traderId);
        if (!cancelResult.IsSuccess &&
            !string.Equals(cancelResult.Message, "Нет активных ордеров для отмены", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Failed to cancel wall orders for trader {TraderId}: {Error}",
                traderId, cancelResult.Message);
        }
    }

    public async Task<Result> ExecuteMarketOrdersAsync(CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await LoadActiveBotsAsync(ct,
                MarketMakerRole.Buyer, MarketMakerRole.Seller);

            if (bots.Count == 0)
                return Result.Ok();

            var marketMask = AggregateRequiredData(modifierCollection.MarketModifiers);
            var marketSnapshots = await LoadSnapshotsAsync(marketMask, bots, ct);

            foreach (var bot in Shuffle(bots))
            {
                var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
                var plan = BuildPlan(domainBot, modifierCollection.MarketModifiers, marketMask, marketSnapshots, bot.Symbol);
                if (plan == null)
                    continue;

                orderCollector.Add(MarketMakerGridMapper.ToCommands(plan));
                await eventPublisher.PublishAsync(new BotPublicOrdersEvent(plan), ct);
            }

            return await PlaceCollectedAsync();
        }, logger, nameof(BotOrchestrator));
    }

    public Task<Result> UpdateWallBotGridsAsync(CancellationToken ct = default)
        => UpdateBotsGridsForRoleAsync(MarketMakerRole.Waller, ct);

    public async Task<Result> RebalanceAllBotsPowerAsync(CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await LoadActiveBotsAsync(ct);
            if (bots.Count == 0)
                return Result.Ok();

            foreach (var bot in bots)
            {
                var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
                var powerModifiers = domainBot.Role == MarketMakerRole.Waller
                    ? modifierCollection.WallerPowerModifiers
                    : modifierCollection.PowerModifiers;
                domainBot.RebalancePower(powerModifiers);
                bot.ActivePower = domainBot.ActivePower;
            }

            await dbContext.SaveChangesAsync(ct);
            return Result.Ok();
        }, logger, nameof(BotOrchestrator));
    }

    public Task<Result<BotEnsuringResult>> EnsureDefaultBotsAsync(CancellationToken ct = default)
        => ServiceErrorHandler.ExecuteAsync(
            () => EnsureDefaultBotsCoreAsync(ct), logger, nameof(BotOrchestrator));

    private async Task<Result<BotEnsuringResult>> EnsureDefaultBotsCoreAsync(CancellationToken ct)
    {
        var symbols = await dbContext.CharacterTokens
            .Where(t => t.IsActive && !string.IsNullOrEmpty(t.Symbol))
            .Select(t => t.Symbol)
            .ToListAsync(ct);

        if (symbols.Count == 0)
            return Result<BotEnsuringResult>.Ok(new BotEnsuringResult(false, 0, 0));

        var existing = await dbContext.MarketMakerBots
            .Where(b => symbols.Contains(b.Symbol))
            .Select(b => new { b.Id, b.Symbol, b.Role, b.TraderId })
            .ToListAsync(ct);

        var botsBySymbolRole = existing
            .GroupBy(b => (b.Symbol, b.Role))
            .ToDictionary(g => g.Key, g => (g.First().Id, g.First().TraderId));

        var allBots = await dbContext.MarketMakerBots
            .Select(b => new { b.TraderId })
            .ToListAsync(ct);
        var traderUsage = allBots
            .GroupBy(b => b.TraderId)
            .ToDictionary(g => g.Key, g => g.Count());
        var botTraderIds = await dbContext.Traders
            .Where(t => traderUsage.Keys.Contains(t.Id) && t.IsBot)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var added = 0;
        var moved = 0;
        foreach (var symbol in symbols)
        {
            foreach (var role in RequiredRoles())
            {
                var outcome = await EnsureBotForRoleAsync(
                    symbol, role, botsBySymbolRole, traderUsage, botTraderIds, ct);
                added += outcome.Added ? 1 : 0;
                moved += outcome.Moved ? 1 : 0;
            }
        }

        var normalized = await NormalizeBotPowersAsync(ct);

        var changed = added > 0 || moved > 0 || normalized > 0;
        return Result<BotEnsuringResult>.Ok(new BotEnsuringResult(changed, added, moved, normalized));
    }

    private async Task<(bool Added, bool Moved)> EnsureBotForRoleAsync(
        string symbol,
        BotRole role,
        Dictionary<(string Symbol, BotRole Role), (long Id, long TraderId)> botsBySymbolRole,
        Dictionary<long, int> traderUsage,
        List<long> botTraderIds,
        CancellationToken ct)
    {
        if (!botsBySymbolRole.TryGetValue((symbol, role), out var bot))
        {
            var regResult = await botRegistration.RegisterBotAsync(symbol, role, DefaultPowerFor(role));
            if (!regResult.IsSuccess)
                throw new InvalidOperationException(regResult.Message);
            return (true, false);
        }

        var dedicated =
            traderUsage.TryGetValue(bot.TraderId, out var count) && count == 1 &&
            botTraderIds.Contains(bot.TraderId);
        if (dedicated)
            return (false, false);

        var traderResult = await botRegistration.CreateDedicatedTraderAsync(symbol, role);
        if (!traderResult.IsSuccess || !traderResult.TryGetData(out var newTraderId))
            throw new InvalidOperationException(traderResult.Message);

        var record = await dbContext.MarketMakerBots.FirstOrDefaultAsync(b => b.Id == bot.Id, ct);
        if (record is null)
            return (false, false);

        var domainBot = MarketMakerGridMapper.ToMarketMaker(record);
        var previousTraderId = domainBot.MoveToTrader(newTraderId);
        record.TraderId = newTraderId;
        await dbContext.SaveChangesAsync(ct);

        await orderCancellationService.CancelAllOrderAsync(previousTraderId);

        if (traderUsage.TryGetValue(bot.TraderId, out var usage) && usage > 1)
            traderUsage[bot.TraderId] = usage - 1;
        else
            traderUsage.Remove(bot.TraderId);
        traderUsage[newTraderId] = 1;
        return (false, true);
    }

    private async Task<int> NormalizeBotPowersAsync(CancellationToken ct)
    {
        var normalized = 0;
        var allBotRecords = await dbContext.MarketMakerBots.ToListAsync(ct);
        foreach (var record in allBotRecords)
        {
            var target = DefaultPowerFor(record.Role);
            if (record.BasePower == target)
                continue;
            record.BasePower = target;
            normalized++;
        }

        if (normalized > 0)
            await dbContext.SaveChangesAsync(ct);
        return normalized;
    }

    private static decimal DefaultPowerFor(BotRole role)
        => role == BotRole.Waller ? 100m : 50m;

    private static BotRole[] RequiredRoles()
        => new[] { BotRole.Buyer, BotRole.Seller, BotRole.Waller };

    private async Task<Result> PlaceCollectedAsync()
    {
        var collections = orderCollector.TakeAll();
        if (collections.Count == 0)
            return Result.Ok();

        var result = await orderCreationService.PlaceCollectedAsync(collections);
        if (!result.IsSuccess)
            logger.LogWarning("Failed to place collected orders: {Error}", result.Message);

        return result;
    }

    private async Task<List<MarketMakerBotRecord>> LoadActiveBotsAsync(CancellationToken ct, params MarketMakerRole[] roles)
    {
        var query = dbContext.MarketMakerBots.Where(b => b.IsActive);

        if (roles.Length > 0)
        {
            var efRoles = roles.Select(r => (BotRole)(int)r).ToArray();
            query = query.Where(b => efRoles.Contains(b.Role));
        }

        return await query.ToListAsync(ct);
    }

    private static IEnumerable<MarketMakerBotRecord> Shuffle(List<MarketMakerBotRecord> bots)
        => bots.OrderBy(_ => Guid.NewGuid());

    private static MarketDataMasks AggregateRequiredData(IReadOnlyCollection<IPlanModify> modifiers)
    {
        var mask = MarketDataMasks.None;
        foreach (var modifier in modifiers)
        {
            mask |= modifier.RequiredMarketData;
        }

        return mask;
    }

    private async Task<IReadOnlyDictionary<string, MarketConditions>> LoadSnapshotsAsync(
        MarketDataMasks mask, IReadOnlyCollection<MarketMakerBotRecord> bots, CancellationToken ct)
    {
        if (mask == MarketDataMasks.None)
        {
            return new Dictionary<string, MarketConditions>();
        }

        var symbols = bots.Select(b => b.Symbol).Distinct().ToArray();
        return await _marketDataProvider.LoadAsync(mask, symbols, ct);
    }

    private static IReadOnlyCollection<CreateMarketOrderCommand>? BuildPlan(
        MarketMakerBot bot,
        IReadOnlyCollection<IPlanModify> modifiers,
        MarketDataMasks mask,
        IReadOnlyDictionary<string, MarketConditions> snapshots,
        string symbol)
    {
        if (mask == MarketDataMasks.None)
        {
            return bot.ExecutePlan(modifiers);
        }

        if (!snapshots.TryGetValue(symbol, out var market))
        {
            return null;
        }

        return bot.ExecutePlan(modifiers, market);
    }
}