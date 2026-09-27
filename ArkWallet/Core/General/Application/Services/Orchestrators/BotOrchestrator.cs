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
    ILogger<BotOrchestrator> logger,
    TimeProvider? timeProvider = null) : IBotOrchestrator
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
                    var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
                    var (balance, portfolioTokens) = domainBot.GetDefaultResources();

                    var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == bot.TraderId, ct);
                    if (trader == null)
                    {
                        logger.LogWarning("Trader {TraderId} not found for bot {BotId}", bot.TraderId, bot.Id);
                        continue;
                    }

                    if (trader.Balance < balance)
                    {
                        trader.Balance = balance;
                        await dbContext.SaveChangesAsync(ct);
                        logger.LogInformation("Trader {TraderId} balance replenished to {Balance}", trader.Id, balance);
                    }

                    var symbols = bot.Symbol == "*"
                        ? (IReadOnlyList<string>)activeTokenSymbols
                        : new List<string> { bot.Symbol };

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

                return Result.Ok();
            });

            return Result.Ok();
        }, logger, nameof(BotOrchestrator));
    }

    public async Task<Result> UpdateBotsGridsAsync(CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await LoadActiveBotsAsync(ct,
                MarketMakerRole.Buyer, MarketMakerRole.Seller);

            if (bots.Count == 0)
                return Result.Ok();

            var gridMask = AggregateRequiredData(modifierCollection.GridModifiers);
            var gridSnapshots = await LoadSnapshotsAsync(gridMask, bots, ct);

            foreach (var bot in Shuffle(bots))
            {
                var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
                var plan = BuildPlan(domainBot, modifierCollection.GridModifiers, gridMask, gridSnapshots, bot.Symbol);
                if (plan == null)
                    continue;

                orderCollector.Add(MarketMakerGridMapper.ToCommands(plan));
                await eventPublisher.PublishAsync(new BotPublicOrdersEvent(plan), ct);
            }

            return await PlaceCollectedAsync(ct);
        }, logger, nameof(BotOrchestrator));
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

            return await PlaceCollectedAsync(ct);
        }, logger, nameof(BotOrchestrator));
    }

    public async Task<Result> UpdateWallBotGridsAsync(CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var bots = await LoadActiveBotsAsync(ct, MarketMakerRole.Waller);
            if (bots.Count == 0)
                return Result.Ok();

            var wallMask = AggregateRequiredData(modifierCollection.WallGridModifiers);
            var wallSnapshots = await LoadSnapshotsAsync(wallMask, bots, ct);

            foreach (var bot in Shuffle(bots))
            {
                var cancelResult = await orderCancellationService.CancelAllOrderAsync(bot.TraderId);
                if (!cancelResult.IsSuccess &&
                    !string.Equals(cancelResult.Message, "Нет активных ордеров для отмены", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Failed to cancel wall orders for trader {TraderId}: {Error}",
                        bot.TraderId, cancelResult.Message);
                }

                var domainBot = MarketMakerGridMapper.ToMarketMaker(bot);
                var plan = BuildPlan(domainBot, modifierCollection.WallGridModifiers, wallMask, wallSnapshots, bot.Symbol);
                if (plan == null)
                    continue;

                orderCollector.Add(MarketMakerGridMapper.ToCommands(plan));
                await eventPublisher.PublishAsync(new BotPublicOrdersEvent(plan), ct);
            }

            return await PlaceCollectedAsync(ct);
        }, logger, nameof(BotOrchestrator));
    }

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
            }

            await dbContext.SaveChangesAsync(ct);
            return Result.Ok();
        }, logger, nameof(BotOrchestrator));
    }

    public async Task<Result> EnsureDefaultBotsAsync(CancellationToken ct = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var symbols = await dbContext.CharacterTokens
                .Where(t => t.IsActive)
                .Select(t => t.Symbol)
                .ToListAsync(ct);

            if (symbols.Count == 0)
                return Result.Ok();

            var existing = await dbContext.MarketMakerBots
                .Where(b => symbols.Contains(b.Symbol))
                .Select(b => new { b.Symbol, b.Role })
                .ToListAsync(ct);

            BotRole[] requiredRoles = { BotRole.Buyer, BotRole.Seller, BotRole.Waller };

            foreach (var symbol in symbols)
            {
                foreach (var role in requiredRoles)
                {
                    if (existing.Any(e => e.Symbol == symbol && e.Role == role))
                        continue;

                    var result = await botRegistration.RegisterBotAsync(symbol, role, 100m);
                    if (!result.IsSuccess)
                        return Result.Fail($"Не удалось создать бота {role} для {symbol}: {result.Message}");
                }
            }

            return Result.Ok();
        }, logger, nameof(BotOrchestrator));
    }

    private async Task<Result> PlaceCollectedAsync(CancellationToken ct)
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

    private static MarketDataMask AggregateRequiredData(IReadOnlyCollection<IPlanModify> modifiers)
    {
        var mask = MarketDataMask.None;
        foreach (var modifier in modifiers)
        {
            mask |= modifier.RequiredMarketData;
        }

        return mask;
    }

    private async Task<IReadOnlyDictionary<string, MarketConditions>> LoadSnapshotsAsync(
        MarketDataMask mask, IReadOnlyCollection<MarketMakerBotRecord> bots, CancellationToken ct)
    {
        if (mask == MarketDataMask.None)
        {
            return new Dictionary<string, MarketConditions>();
        }

        var symbols = bots.Select(b => b.Symbol).Distinct().ToArray();
        return await _marketDataProvider.LoadAsync(mask, symbols, ct);
    }

    private static IReadOnlyCollection<CreateMarketOrderCommand>? BuildPlan(
        MarketMakerBot bot,
        IReadOnlyCollection<IPlanModify> modifiers,
        MarketDataMask mask,
        IReadOnlyDictionary<string, MarketConditions> snapshots,
        string symbol)
    {
        if (mask == MarketDataMask.None)
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