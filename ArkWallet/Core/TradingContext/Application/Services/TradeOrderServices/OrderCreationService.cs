using ArkWallet.Core.TradingContext.Application.Dtos;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Dtos;
using ArkWallet.Core.General.Application.Contracts.Other;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.General.Domain.Common;
using ValueObjects = global::ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.Events;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Records = global::ArkWallet.Infrastructure.Data;
namespace ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;

internal class OrderCreationService(
    ArkWalletDbContext dbContext,
    TradingEngine tradingEngine,
    IEventPublisher eventPublisher,
    ITaskDispatcher taskDispatcher,
    ILogger<OrderCreationService> logger) : IOrderCreationService
{
    public async Task<Result<OrderCreationData>> CreateOrderAsync(CreateOrderCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var context = await PrepareSingleTradingContextAsync(command);

                await tradingEngine.ProcessOrder(context);

                SyncContextMappings(context, dbContext);
                await dbContext.SaveChangesAsync();

                await NotifyAsync(context);

                return Result<OrderCreationData>.Ok(MapOrderCreationResults(context)[0]);
            });
        }, logger, nameof(OrderCreationService));
    }

    public async Task<Result<List<OrderCreationData>>> CreateOrdersAsync(IEnumerable<CreateOrderCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var commandList = commands.ToList();
                if (commandList.Count == 0)
                    return Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>());

                var groups = commandList
                    .GroupBy(c => new { c.Direction, c.Symbol })
                    .ToList();

                var allResults = new List<OrderCreationData>();

                foreach (var group in groups)
                    await ProcessGroupAsync(group, allResults);

                return Result<List<OrderCreationData>>.Ok(allResults);
            });
        }, logger, nameof(OrderCreationService));
    }

    public async Task<Result> PlaceCollectedAsync(IReadOnlyCollection<IReadOnlyCollection<CreateOrderCommand>> collections)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var candidates = collections
                .Where(c => c is { Count: > 0 })
                .ToList();

            if (candidates.Count == 0)
                return Result.Ok();

            var placedGroups = 0;
            var failedGroups = 0;
            var errors = new List<string>();

            foreach (var collection in candidates)
            {
                var result = await CreateOrdersAsync(collection);

                if (result.IsSuccess)
                {
                    placedGroups++;
                }
                else
                {
                    failedGroups++;
                    errors.Add(result.Message);

                    // Without the symbol and the traders behind the collection a failure reads as a
                    // bare reason, and there is no way to tell which bot stopped trading from the log.
                    var symbol = collection.First().Symbol;
                    var direction = collection.First().Direction;
                    var traderIds = collection.Select(c => c.TraderId).Distinct().ToArray();
                    logger.LogWarning(
                        "Коллекция не размещена: {Direction} {Symbol}, ордеров {OrderCount}, "
                        + "трейдеры {TraderIds}, сумма {Total}. Причина: {Reason}",
                        direction, symbol, collection.Count,
                        string.Join(",", traderIds),
                        collection.Sum(c => (decimal)c.Price * c.Quantity),
                        result.Message);
                }
            }

            return failedGroups == 0
                ? Result.Ok()
                : Result.Fail($"Не размещено коллекций: {failedGroups} из {candidates.Count}. Ошибки: {string.Join("; ", errors)}");
        }, logger, nameof(OrderCreationService));
    }

    private async Task ProcessGroupAsync(
        IEnumerable<CreateOrderCommand> groupCommands,
        List<OrderCreationData> allResults)
    {
        var context = await PrepareGroupTradingContextAsync(groupCommands);

        await tradingEngine.ProcessOrders(context);

        SyncContextMappings(context, dbContext);
        await dbContext.SaveChangesAsync();

        allResults.AddRange(MapOrderCreationResults(context));

        await NotifyAsync(context);
    }

    private async Task<TradingEngineContext> PrepareSingleTradingContextAsync(CreateOrderCommand command)
    {
        var orderType = NormalizeDirection(command.Direction) == OrderDirections.Buy
            ? ValueObjects.OrderType.Buy
            : ValueObjects.OrderType.Sell;

        var order = Records.TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity);

        await dbContext.LockTradersAsync([order.TraderId]);
        await dbContext.LockTokenAsync(order.CharacterTokenId);

        var takerIds = await GetTakerIdsForMatchingAsync(order);

        var additionalTakerIds = takerIds.Except([order.TraderId]).ToArray();
        if (additionalTakerIds.Length > 0)
            await dbContext.LockTradersAsync(additionalTakerIds);

        var token = await dbContext.CharacterTokens.FindAsync(command.Symbol)
            ?? throw new InvalidOperationException("Токена не существует");

        var validationResult = await ValidateFullOrderAsync(command);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(validationResult.Message);

        var counterType = order.IsLong ? ValueObjects.OrderType.Sell : ValueObjects.OrderType.Buy;

        var activeOrders = await dbContext.TradeOrders
            .Include(o => o.Trader)
            .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                       o.Status == ValueObjects.OrderStatus.Active &&
                       o.Type == counterType &&
                       (order.IsLong ? o.Price <= order.Price : o.Price >= order.Price))
            .ToArrayAsync();

        var traderIds = activeOrders.Select(o => o.TraderId)
            .Append(order.TraderId).Distinct().ToArray();

        var portfolioItems = await dbContext.PortfolioItems
            .Where(p => traderIds.Contains(p.TraderId) && p.CharacterTokenId == order.CharacterTokenId)
            .ToArrayAsync();

        var traders = new Dictionary<long, Records.Trader>();
        foreach (var o in activeOrders)
            if (o.Trader != null) traders.TryAdd(o.TraderId, o.Trader);

        var newTrader = await dbContext.Traders.FindAsync(command.TraderId)
            ?? throw new InvalidOperationException("Пользователя не существует");

        await EnsureOrderLimitAsync([command.TraderId]);

        traders.TryAdd(newTrader.Id, newTrader);

        var portfolios = portfolioItems.ToDictionary(p => p.TraderId);

        return await TradingContextMapper.BuildContext(
            new[] { command },
            order.IsLong,
            traders,
            activeOrders,
            portfolios,
            token,
            eventPublisher);
    }

    private async Task<TradingEngineContext> PrepareGroupTradingContextAsync(IEnumerable<CreateOrderCommand> commands)
    {
        var commandList = commands.ToList();
        if (commandList.Count == 0)
            throw new InvalidOperationException("Нет команд для обработки");

        var validationResult = await ValidateFullOrdersAsync(commandList);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(validationResult.Message);

        var orders = CreateOrderEntities(commandList);
        var isBuy = orders[0].IsLong;
        var targetOrder = SelectTargetOrder(orders, isBuy);

        var commandTraderIds = commandList.Select(c => c.TraderId).Distinct().ToArray();
        await dbContext.LockTradersAsync(commandTraderIds);
        await dbContext.LockTokenAsync(targetOrder.CharacterTokenId);

        var takerIds = await GetTakerIdsForMatchingAsync(targetOrder);

        var additionalTakerIds = takerIds.Except(commandTraderIds).ToArray();
        if (additionalTakerIds.Length > 0)
            await dbContext.LockTradersAsync(additionalTakerIds);

        var token = await GetTokenOrThrowAsync(commandList[0].Symbol);

        var activeOrders = await LoadActiveCounterOrdersAsync(targetOrder, isBuy);

        var traders = await LoadGroupTradersAsync(activeOrders, commandList);

        await EnsureOrderLimitAsync(commands.Select(c => c.TraderId).Distinct().ToArray());

        var portfolios = await LoadGroupPortfoliosAsync(activeOrders, commandList, targetOrder.CharacterTokenId);

        return await TradingContextMapper.BuildContext(
            commandList,
            isBuy,
            traders,
            activeOrders,
            portfolios,
            token,
            eventPublisher);
    }

    /// <summary>Creates trade order aggregates from the given commands.</summary>
    private static List<Records.TradeOrder> CreateOrderEntities(List<CreateOrderCommand> commandList)
    {
        var orders = new List<Records.TradeOrder>(commandList.Count);
        foreach (var command in commandList)
        {
            var orderType = NormalizeDirection(command.Direction) == OrderDirections.Buy
                ? ValueObjects.OrderType.Buy
                : ValueObjects.OrderType.Sell;

            orders.Add(Records.TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity));
        }

        return orders;
    }

    /// <summary>Selects the order defining matching bounds: highest priced for buys, lowest priced for sells.</summary>
    private static Records.TradeOrder SelectTargetOrder(List<Records.TradeOrder> orders, bool isBuy)
    {
        return isBuy
            ? orders.OrderByDescending(o => o.Price).First()
            : orders.OrderBy(o => o.Price).First();
    }

    /// <summary>Loads the token by symbol or throws when it does not exist.</summary>
    private async Task<Records.CharacterToken> GetTokenOrThrowAsync(string symbol)
    {
        return await dbContext.CharacterTokens.FindAsync(symbol)
            ?? throw new InvalidOperationException("Токена не существует");
    }

    /// <summary>Loads active counter-orders matching the target order price for the given direction.</summary>
    private async Task<Records.TradeOrder[]> LoadActiveCounterOrdersAsync(Records.TradeOrder targetOrder, bool isBuy)
    {
        var counterType = isBuy ? ValueObjects.OrderType.Sell : ValueObjects.OrderType.Buy;

        return await dbContext.TradeOrders
            .Include(o => o.Trader)
            .Where(o => o.CharacterTokenId == targetOrder.CharacterTokenId &&
                       o.Status == ValueObjects.OrderStatus.Active &&
                       o.Type == counterType &&
                       (isBuy ? o.Price <= targetOrder.Price : o.Price >= targetOrder.Price))
            .ToArrayAsync();
    }

    /// <summary>Builds the trader dictionary from counter-order owners and command traders, loading missing ones.</summary>
    private async Task<Dictionary<long, Records.Trader>> LoadGroupTradersAsync(
        Records.TradeOrder[] activeOrders,
        List<CreateOrderCommand> commandList)
    {
        var traders = new Dictionary<long, Records.Trader>();
        foreach (var o in activeOrders)
            if (o.Trader != null) traders.TryAdd(o.TraderId, o.Trader);

        var missingTraderIds = commandList
            .Select(c => c.TraderId)
            .Where(id => !traders.ContainsKey(id))
            .Distinct()
            .ToArray();

        if (missingTraderIds.Length > 0)
        {
            var loaded = await dbContext.Traders
                .Where(t => missingTraderIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            foreach (var id in missingTraderIds)
            {
                if (!loaded.TryGetValue(id, out var trader))
                    throw new InvalidOperationException("Пользователя не существует");
                traders[id] = trader;
            }
        }

        return traders;
    }

    /// <summary>Loads portfolio items for all involved traders and the token.</summary>
    private async Task<Dictionary<long, Records.PortfolioItem>> LoadGroupPortfoliosAsync(
        Records.TradeOrder[] activeOrders,
        List<CreateOrderCommand> commandList,
        string characterTokenId)
    {
        var traderIds = activeOrders.Select(o => o.TraderId)
            .Concat(commandList.Select(c => c.TraderId)).Distinct().ToArray();

        var portfolioItems = await dbContext.PortfolioItems
            .Where(p => traderIds.Contains(p.TraderId) && p.CharacterTokenId == characterTokenId)
            .ToArrayAsync();

        return portfolioItems.ToDictionary(p => p.TraderId);
    }

    private async Task<long[]> GetTakerIdsForMatchingAsync(Records.TradeOrder order)
    {
        return order.IsLong
            ? await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == ValueObjects.OrderStatus.Active &&
                           o.Type == ValueObjects.OrderType.Sell &&
                           o.Price <= order.Price)
                .Select(o => o.TraderId)
                .Distinct()
                .ToArrayAsync()
            : await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == ValueObjects.OrderStatus.Active &&
                           o.Type == ValueObjects.OrderType.Buy &&
                           o.Price >= order.Price)
                .Select(o => o.TraderId)
                .Distinct()
                .ToArrayAsync();
    }

    private async Task NotifyAsync(TradingEngineContext context)
    {
        var ordersToNotify = CollectFilledOrderRecords(context, dbContext);

        if (ordersToNotify.Count > 0)
        {
            var traders = context.Traders.Values
                .Select(t => dbContext.Traders.Local.FirstOrDefault(tr => tr.Id == t.Id))
                .Where(tr => tr != null)
                .Select(tr => tr!)
                .ToList();

            await taskDispatcher.SendTaskAsync("notification",
                NotificationEvent.FromOrderList(ordersToNotify, traders, logger));
        }
    }

    private static async Task<ValidationResult> ValidateFullOrderAsync(CreateOrderCommand request)
    {
        if (request.Price <= 0)
            return ValidationResult.Failed("Цена должна быть больше 0");

        if (request.Quantity <= 0)
            return ValidationResult.Failed("Количество должно быть больше 0");

        return await Task.FromResult(ValidationResult.Success());
    }

    private static async Task<ValidationResult> ValidateFullOrdersAsync(List<CreateOrderCommand> requests)
    {
        if (requests.Count == 0)
            return ValidationResult.Success();

        foreach (var request in requests)
        {
            if (request.Price <= 0)
                return ValidationResult.Failed("Цена должна быть больше 0");

            if (request.Quantity <= 0)
                return ValidationResult.Failed("Количество должно быть больше 0");
        }

        return ValidationResult.Success();
    }

    private async Task<int> GetMaxActiveOrdersAsync(long traderId)
    {
        var trader = await dbContext.Traders
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == traderId);

        if (trader is null)
            throw new InvalidOperationException("Пользователя не существует");

        if (trader.Subscription is not null &&
            (trader.SubscriptionExpiresAtUtc is null || trader.SubscriptionExpiresAtUtc.Value > DateTime.UtcNow))
            return trader.Subscription.MaxOrders;

        return 5;
    }

    private async Task EnsureOrderLimitAsync(IEnumerable<long> traderIds)
    {
        foreach (var traderId in traderIds.Distinct())
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == traderId);
            if (trader?.IsBot == true)
                continue;

            var maxOrders = await GetMaxActiveOrdersAsync(traderId);
            var activeCount = await dbContext.TradeOrders.CountAsync(o =>
                o.TraderId == traderId && o.Status == ValueObjects.OrderStatus.Active);

            if (activeCount >= maxOrders)
                throw new InvalidOperationException($"Достигнут лимит активных ордеров: {maxOrders}");
        }
    }

    private static string? NormalizeDirection(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
            return null;

        var trimmed = direction.Trim();
        if (trimmed.Equals(OrderDirections.Buy, StringComparison.CurrentCultureIgnoreCase))
            return OrderDirections.Buy;
        if (trimmed.Equals(OrderDirections.Sell, StringComparison.CurrentCultureIgnoreCase))
            return OrderDirections.Sell;

        return null;
    }

    private static void SyncContextMappings(TradingEngineContext context, ArkWalletDbContext dbContext)
    {
        TradingContextMapper.SyncTradersAndPortfolios(context, dbContext);
        TradingContextMapper.SyncToken(context, dbContext);
    }

    private static List<OrderCreationData> MapOrderCreationResults(TradingEngineContext context) =>
        TradingContextMapper.ToOrderCreationResults(context);

    private static List<Records.TradeOrder> CollectFilledOrderRecords(
        TradingEngineContext context,
        ArkWalletDbContext dbContext) =>
        TradingContextMapper.CollectFilledOrderRecords(context, dbContext);
}
