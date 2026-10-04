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
using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<long, BackoffState> _botBackoff = new();
#pragma warning restore S107

    private const int MaxConsecutiveFailures = 5;
    private static readonly TimeSpan CooldownInterval = TimeSpan.FromSeconds(60);

    private sealed record BackoffState(int ConsecutiveFailures, DateTime LastCheckUtc);

    private bool CanExecuteBot(MarketMakerBotRecord bot, BotResources resources)
    {
        var traderId = bot.TraderId;
        var current = _botBackoff.GetOrAdd(traderId, _ => new BackoffState(0, DateTime.MinValue));

        if (current.ConsecutiveFailures >= MaxConsecutiveFailures && (DateTime.UtcNow - current.LastCheckUtc) < CooldownInterval)
        {
            logger.LogDebug("Bot {BotId} ({TraderId}) on cooldown: {Failures} consecutive failures, skipped", bot.Id, traderId, current.ConsecutiveFailures);
            return false;
        }

        if (bot.Role == BotRole.Seller || bot.Role == BotRole.Waller)
        {
            var freeQty = resources.SellerQuantities.GetValueOrDefault((traderId, bot.Symbol));
            if (freeQty <= 0)
            {
                logger.LogDebug("Bot {BotId} ({TraderId}) has no available portfolio for symbol {Symbol}, skipped", bot.Id, traderId, bot.Symbol);
                return false;
            }

            return true;
        }

        var balance = resources.BuyerBalances.GetValueOrDefault(traderId);
        if (balance <= 0m)
        {
            logger.LogDebug("Bot {BotId} ({TraderId}) has zero balance, skipped", bot.Id, traderId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resources available to bots this tick: free token quantities for seller/waller bots
    /// and spendable balances for buyer bots. Loaded in two batched queries so the per-bot
    /// check in the placement loop does not cause N+1 database traffic.
    /// </summary>
    private async Task<BotResources> LoadBotResourcesAsync(
        IReadOnlyCollection<MarketMakerBotRecord> bots, CancellationToken ct)
    {
        var sellerQuantities = await LoadSellerPortfolioAsync(bots, ct);

        var buyerTraders = bots
            .Where(b => b.Role == BotRole.Buyer)
            .Select(b => b.TraderId)
            .Distinct()
            .ToList();

        var buyerBalances = buyerTraders.Count > 0
            ? await dbContext.Traders
                .Where(t => buyerTraders.Contains(t.Id))
                .Where(t => t.Balance > 0)
                .ToDictionaryAsync(t => t.Id, t => t.Balance, ct)
            : new Dictionary<long, decimal>();

        return new BotResources(sellerQuantities, buyerBalances);
    }

    private sealed record BotResources(
        Dictionary<(long TraderId, string Symbol), int> SellerQuantities,
        Dictionary<long, decimal> BuyerBalances);

    /// <summary>
    /// Loads free token quantities available to seller/waller bots, keyed by (TraderId, Symbol).
    /// Single query for the whole batch — a per-bot query would cause N+1 on every tick.
    /// </summary>
    private async Task<Dictionary<(long TraderId, string Symbol), int>> LoadSellerPortfolioAsync(
        IReadOnlyCollection<MarketMakerBotRecord> bots, CancellationToken ct)
    {
        var traderIds = bots
            .Where(b => b.Role == BotRole.Seller || b.Role == BotRole.Waller)
            .Select(b => b.TraderId)
            .Distinct()
            .ToList();

        if (traderIds.Count == 0)
            return [];

        var symbols = bots.Select(b => b.Symbol).Distinct().ToList();

        // CharacterTokenId stores the token symbol (see UpdatingService.CreateOrUpdatePortfolioAsync),
        // so it is compared directly against the bot symbol — no CharacterToken navigation needed.
        return await dbContext.PortfolioItems
            .Where(pi => traderIds.Contains(pi.TraderId))
            .Where(pi => symbols.Contains(pi.CharacterTokenId))
            .Where(pi => (pi.Quantity - pi.SellingQuantity - pi.ReserveQuantity) > 0)
            .GroupBy(pi => new { pi.TraderId, pi.CharacterTokenId })
            .Select(g => new
            {
                g.Key.TraderId,
                Symbol = g.Key.CharacterTokenId,
                FreeQty = g.Sum(pi => pi.Quantity - pi.SellingQuantity - pi.ReserveQuantity)
            })
            .ToDictionaryAsync(
                x => (x.TraderId, x.Symbol),
                x => x.FreeQty,
                ct);
    }

    private void RecordBotSkip(long botTraderId)
    {
        var current = _botBackoff.GetOrAdd(botTraderId, _ => new BackoffState(0, DateTime.MinValue));
        if ((DateTime.UtcNow - current.LastCheckUtc) >= CooldownInterval)
        {
            _botBackoff[botTraderId] = new BackoffState(1, DateTime.UtcNow);
        }
        else
        {
            _botBackoff[botTraderId] = new BackoffState(current.ConsecutiveFailures + 1, DateTime.UtcNow);
        }
    }

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
        var buyerResult = await UpdateBotsGridsForRoleAsync(MarketMakerRole.Buyer, ct: ct);
        if (!buyerResult.IsSuccess)
            return buyerResult;

        return await UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller, ct: ct);
    }

public async Task<Result> UpdateBotsGridsForRoleAsync(MarketMakerRole role, bool cancelExistingOrders = false, CancellationToken ct = default)
        {
            return await ServiceErrorHandler.ExecuteAsync(async () =>
            {
                var bots = await LoadActiveBotsAsync(ct, role);
                if (bots.Count == 0)
                    return Result.Ok();

                var isWall = role == MarketMakerRole.Waller;
                var shouldCancelOrders = isWall || cancelExistingOrders;
                var modifiers = isWall ? modifierCollection.WallGridModifiers : modifierCollection.GridModifiers;

                var mask = AggregateRequiredData(modifiers);
                var snapshots = await LoadSnapshotsAsync(mask, bots, ct);

                var resources = await LoadBotResourcesAsync(bots, ct);

                foreach (var bot in Shuffle(bots))
                {
                    if (shouldCancelOrders)
                        await CancelBotOrdersAsync(bot.TraderId);

                    if (!CanExecuteBot(bot, resources))
                    {
                        RecordBotSkip(bot.TraderId);
                        continue;
                    }

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

    private async Task CancelBotOrdersAsync(long traderId)
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

            var resources = await LoadBotResourcesAsync(bots, ct);

            foreach (var bot in Shuffle(bots))
            {
                if (!CanExecuteBot(bot, resources))
                {
                    RecordBotSkip(bot.TraderId);
                    continue;
                }

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
        => UpdateBotsGridsForRoleAsync(MarketMakerRole.Waller, ct: ct);

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
            .Select(b => new { b.Id, b.TraderId })
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

        // The loop above only visits records on active tokens with a required role. A record whose
        // token was deactivated, or whose role is no longer required, would otherwise keep a shared
        // or non-bot trader forever, so sweep every remaining record on its own.
        var notDedicatedIds = await (
                from b in dbContext.MarketMakerBots
                join t in dbContext.Traders on b.TraderId equals t.Id into matched
                from t in matched.DefaultIfEmpty()
                where t == null || !t.IsBot
                      || dbContext.MarketMakerBots.Count(x => x.TraderId == b.TraderId) != 1
                select b.Id)
            .ToListAsync(ct);

        foreach (var botId in notDedicatedIds)
        {
            if (await MoveBotToDedicatedTraderAsync(botId, traderUsage, botTraderIds, ct))
                moved++;
        }

        var orphansRemoved = await RemoveOrphanBotsAsync(ct);

        var normalized = await NormalizeBotPowersAsync(ct);

        var changed = added > 0 || moved > 0 || normalized > 0 || orphansRemoved > 0;
        return Result<BotEnsuringResult>.Ok(
            new BotEnsuringResult(changed, added, moved, normalized, orphansRemoved));
    }

    /// <summary>
    /// Removes bot traders that no market maker record refers to. Composition is fixed at three
    /// records per active token, so a bot without a record is surplus left over from an older
    /// registration scheme and would otherwise sit forever: nothing selects it, but it keeps
    /// balance, portfolio and open orders that are never managed again.
    /// </summary>
    private async Task<int> RemoveOrphanBotsAsync(CancellationToken ct)
    {
        var orphans = await dbContext.Traders
            .Where(t => t.IsBot && !dbContext.MarketMakerBots.Any(b => b.TraderId == t.Id))
            .ToListAsync(ct);

        foreach (var trader in orphans)
        {
            await orderCancellationService.CancelAllOrderAsync(trader.Id);
            dbContext.Traders.Remove(trader);
        }

        if (orphans.Count > 0)
            await dbContext.SaveChangesAsync(ct);

        return orphans.Count;
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

        var movedOk = await MoveBotToDedicatedTraderAsync(bot.Id, traderUsage, botTraderIds, ct);
        return (false, movedOk);
    }

    private async Task<bool> MoveBotToDedicatedTraderAsync(
        long botId,
        Dictionary<long, int> traderUsage,
        List<long> botTraderIds,
        CancellationToken ct)
    {
        var record = await dbContext.MarketMakerBots.FirstOrDefaultAsync(b => b.Id == botId, ct);
        if (record is null)
            return false;

        var traderResult = await botRegistration.CreateDedicatedTraderAsync(record.Symbol, record.Role);
        if (!traderResult.IsSuccess || !traderResult.TryGetData(out var newTraderId))
            throw new InvalidOperationException(traderResult.Message);

        var domainBot = MarketMakerGridMapper.ToMarketMaker(record);
        var previousTraderId = domainBot.MoveToTrader(newTraderId);
        record.TraderId = newTraderId;
        await dbContext.SaveChangesAsync(ct);

        await orderCancellationService.CancelAllOrderAsync(previousTraderId);

        if (traderUsage.TryGetValue(previousTraderId, out var usage))
        {
            if (usage > 1)
                traderUsage[previousTraderId] = usage - 1;
            else
                traderUsage.Remove(previousTraderId);
        }

        traderUsage[newTraderId] = 1;
        if (!botTraderIds.Contains(newTraderId))
            botTraderIds.Add(newTraderId);

        return true;
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