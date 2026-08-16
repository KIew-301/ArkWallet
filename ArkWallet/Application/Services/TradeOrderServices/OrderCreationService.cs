using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.TradingContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Records = global::ArkWallet.Domain.Entities;
using ValueObjects = global::ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Services.TradeOrderServices;

internal class OrderCreationService(
    ArkWalletDbContext dbContext,
    TradingEngine tradingEngine,
    IOrderValidationService orderValidationService,
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

                SyncTradersAndPortfolios(context);
                await dbContext.SaveChangesAsync();

                await NotifyAsync(context);

                var order = context.NewOrders[0];
                return Result<OrderCreationData>.Ok(new(order.IsFilled(), OrderDto.FromAggregate(order, order.TraderId)));
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
                var allContexts = new List<TradingContext>();

                foreach (var group in groups)
                    await ProcessGroupAsync(group, allContexts, allResults);

                foreach (var context in allContexts)
                {
                    SyncTradersAndPortfolios(context);
                    await dbContext.SaveChangesAsync();
                    await NotifyAsync(context);
                }

                return Result<List<OrderCreationData>>.Ok(allResults);
            });
        }, logger, nameof(OrderCreationService));
    }

    private async Task ProcessGroupAsync(
        IEnumerable<CreateOrderCommand> groupCommands,
        List<TradingContext> allContexts,
        List<OrderCreationData> allResults)
    {
        var context = await PrepareGroupTradingContextAsync(groupCommands);

        await tradingEngine.ProcessOrders(context);

        allContexts.Add(context);

        foreach (var order in context.NewOrders)
        {
            allResults.Add(new(order.IsFilled(), OrderDto.FromAggregate(order, order.TraderId)));
        }
    }

    private void SyncTradersAndPortfolios(TradingContext context)
    {
        foreach (var trader in context.Traders.Values)
        {
            var trackedTrader = dbContext.Traders.Local.FirstOrDefault(t => t.TelegramId == trader.Id);
            if (trackedTrader != null)
                TradingContextMapper.ApplyTo(trackedTrader, trader);

            foreach (var item in trader.Portfolio)
            {
                var trackedItem = dbContext.PortfolioItems.Local
                    .FirstOrDefault(p => p.Id == item.Id);

                if (trackedItem is null)
                    dbContext.PortfolioItems.Add(TradingContextMapper.ToPortfolio(trader.Id, item));
                else
                    TradingContextMapper.ApplyTo(trackedItem, item);
            }
        }
    }

    private async Task<TradingContext> PrepareSingleTradingContextAsync(CreateOrderCommand command)
    {
        var orderType = OrderValidationService.NormalizeDirection(command.Direction) == OrderDirections.Buy
            ? ValueObjects.OrderType.Buy
            : ValueObjects.OrderType.Sell;

        var order = Records.TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity);

        var takerIds = await GetTakerIdsForMatchingAsync(order);

        await dbContext.LockTradersAsync(takerIds.Append(order.TraderTelegramId));
        await dbContext.LockTokenAsync(order.CharacterTokenId);

        var token = await dbContext.CharacterTokens.FindAsync(command.Symbol);

        if (token == null)
            throw new InvalidOperationException("Токена не существует");

        var activeOrders = await GetActiveOrdersForMatchingAsync(order);

        var validationResult = await orderValidationService.ValidateFullOrderAsync(command);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(validationResult.Message);

        if (await dbContext.Traders.FindAsync(command.TraderId) == null)
            throw new InvalidOperationException("Пользователя не существует");

        var traders = await GetTradersForOrderAsync(activeOrders, order.TraderTelegramId);

        var portfolios = await GetPortfoliosForTradersAsync(order.CharacterTokenId, traders.Keys);

        return await BuildTradingContext(
            new[] { command },
            order.IsLong(),
            traders,
            activeOrders,
            portfolios,
            token,
            eventPublisher);
    }

    private async Task<TradingContext> PrepareGroupTradingContextAsync(IEnumerable<CreateOrderCommand> commands)
    {
        var commandList = commands.ToList();
        if (commandList.Count == 0)
            throw new InvalidOperationException("Нет команд для обработки");

        var firstCommand = commandList[0];

        var validationResult = await orderValidationService.ValidateFullOrdersAsync(commandList);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(validationResult.Message);

        var orders = new List<Records.TradeOrder>();
        foreach (var command in commandList)
        {
            var orderType = OrderValidationService.NormalizeDirection(command.Direction) == OrderDirections.Buy
                ? ValueObjects.OrderType.Buy
                : ValueObjects.OrderType.Sell;

            orders.Add(Records.TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity));
        }

        var isBuy = orders[0].IsLong();

        var targetOrder = isBuy
            ? orders.OrderByDescending(o => o.Price).First()
            : orders.OrderBy(o => o.Price).First();

        var takerIds = await GetTakerIdsForMatchingAsync(targetOrder);

        var lockTraderIds = takerIds.Concat(commandList.Select(c => c.TraderId)).Distinct().ToArray();
        await dbContext.LockTradersAsync(lockTraderIds);
        await dbContext.LockTokenAsync(targetOrder.CharacterTokenId);

        var token = await dbContext.CharacterTokens.FindAsync(firstCommand.Symbol)
            ?? throw new InvalidOperationException("Токена не существует");

        var activeOrders = await GetActiveOrdersForMatchingAsync(targetOrder);

        var trader = await dbContext.Traders.FindAsync(firstCommand.TraderId)
            ?? throw new InvalidOperationException("Пользователя не существует");

        var traders = await GetTradersForOrderAsync(activeOrders, trader.TelegramId);

        foreach (var command in commandList)
        {
            if (traders.ContainsKey(command.TraderId))
                continue;

            var commandTrader = await dbContext.Traders.FindAsync(command.TraderId)
                ?? throw new InvalidOperationException("Пользователя не существует");
            traders[command.TraderId] = commandTrader;
        }

        var portfolios = await GetPortfoliosForTradersAsync(targetOrder.CharacterTokenId, traders.Keys);

        return await BuildTradingContext(
            commandList,
            isBuy,
            traders,
            activeOrders,
            portfolios,
            token,
            eventPublisher);
    }

    private static async Task<TradingContext> BuildTradingContext(
        IReadOnlyCollection<CreateOrderCommand> commands,
        bool isBuy,
        Dictionary<long, Records.Trader> oldTraders,
        Records.TradeOrder[] activeOrders,
        Dictionary<long, Records.PortfolioItem> oldPortfolios,
        Records.CharacterToken oldToken,
        IEventPublisher eventPublisher)
    {
        var context = new TradingContext
        {
            Token = TradingContextMapper.ToToken(oldToken),
            EventPublisher = eventPublisher,
        };
        context.Token.SetEventPublisher(eventPublisher);

        var traderIds = commands.Select(c => c.TraderId)
            .Concat(activeOrders.Select(o => o.TraderTelegramId))
            .Distinct();

        foreach (var traderId in traderIds)
        {
            if (!oldTraders.TryGetValue(traderId, out var oldTrader))
                throw new InvalidOperationException("Трейдер не найден");

            var trader = TradingContextMapper.ToTrader(oldTrader);
            trader.SetEventPublisher(eventPublisher);
            context.Traders[traderId] = trader;

            if (oldPortfolios.TryGetValue(traderId, out var oldPortfolio))
                trader.AttachPortfolio(TradingContextMapper.ToPortfolioItem(oldPortfolio));
        }

        foreach (var oldOrder in activeOrders)
        {
            var order = TradingContextMapper.ToOrder(oldOrder);
            order.SetEventPublisher(eventPublisher);
            context.ExistingOrders.Add(order);

            if (context.Traders.TryGetValue(oldOrder.TraderTelegramId, out var owner))
                owner.AttachOrder(order);
        }

        var orderType = isBuy ? OrderType.Buy : OrderType.Sell;

        var placedOrders = new List<Order>();
        foreach (var command in commands)
        {
            var trader = context.Traders[command.TraderId];
            placedOrders.Add(await trader.PlaceOrder(orderType, command.Symbol, command.Price, command.Quantity));
        }

        context.NewOrders = isBuy
            ? placedOrders.OrderBy(o => o.Price).ToList()
            : placedOrders.OrderByDescending(o => o.Price).ToList();

        return context;
    }

    private async Task<Records.TradeOrder[]> GetActiveOrdersForMatchingAsync(Records.TradeOrder order)
    {
        return order.IsLong()
            ? await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == ValueObjects.OrderStatus.Active &&
                           o.Type == ValueObjects.OrderType.Sell &&
                           o.Price <= order.Price)
                .ToArrayAsync()
            : await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == ValueObjects.OrderStatus.Active &&
                           o.Type == ValueObjects.OrderType.Buy &&
                           o.Price >= order.Price)
                .ToArrayAsync();
    }

    private async Task<long[]> GetTakerIdsForMatchingAsync(Records.TradeOrder order)
    {
        return order.IsLong()
            ? await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == ValueObjects.OrderStatus.Active &&
                           o.Type == ValueObjects.OrderType.Sell &&
                           o.Price <= order.Price)
                .Select(o => o.TraderTelegramId)
                .Distinct()
                .ToArrayAsync()
            : await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == ValueObjects.OrderStatus.Active &&
                           o.Type == ValueObjects.OrderType.Buy &&
                           o.Price >= order.Price)
                .Select(o => o.TraderTelegramId)
                .Distinct()
                .ToArrayAsync();
    }

    private async Task<Dictionary<long, Records.Trader>> GetTradersForOrderAsync(Records.TradeOrder[] activeOrders, long newOrderTraderId)
    {
        var traderIds = activeOrders.Select(o => o.TraderTelegramId).Append(newOrderTraderId).Distinct().ToHashSet();

        var traders = new Dictionary<long, Records.Trader>();
        foreach (var traderId in traderIds)
        {
            var tracked = dbContext.Traders.Local.FirstOrDefault(t => t.TelegramId == traderId);
            if (tracked != null)
                traders[traderId] = tracked;
        }

        var missingIds = traderIds.Except(traders.Keys).ToArray();
        if (missingIds.Length > 0)
        {
            var fromDb = await dbContext.Traders.Where(t => missingIds.Contains(t.TelegramId)).ToArrayAsync();
            foreach (var trader in fromDb)
                traders[trader.TelegramId] = trader;
        }

        return traders;
    }

    private async Task<Dictionary<long, Records.PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
    {
        var ids = traderIds.Distinct().ToHashSet();

        var portfolios = new Dictionary<long, Records.PortfolioItem>();
        foreach (var traderId in ids)
        {
            var tracked = dbContext.PortfolioItems.Local
                .FirstOrDefault(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            if (tracked != null)
                portfolios[traderId] = tracked;
        }

        var missingIds = ids.Except(portfolios.Keys).ToArray();
        if (missingIds.Length > 0)
        {
            var fromDb = await dbContext.PortfolioItems
                .Where(p => missingIds.Contains(p.TraderTelegramId) && p.CharacterTokenId == symbol)
                .ToArrayAsync();
            foreach (var portfolio in fromDb)
                portfolios[portfolio.TraderTelegramId] = portfolio;
        }

        return portfolios;
    }

    private async Task NotifyAsync(TradingContext context)
    {
        var ordersToNotify = new List<Records.TradeOrder>();

        foreach (var order in context.ExistingOrders.Where(o => o.Status == OrderStatus.Filled))
        {
            var tracked = dbContext.TradeOrders.Local.FirstOrDefault(o => o.Id == order.Id);
            if (tracked != null)
                ordersToNotify.Add(tracked);
        }

        foreach (var order in context.NewOrders.Where(o => o.IsFilled()))
        {
            var tracked = dbContext.TradeOrders.Local.FirstOrDefault(o => o.Id == order.Id);
            if (tracked != null)
                ordersToNotify.Add(tracked);
        }

        if (ordersToNotify.Count > 0)
        {
            var traders = context.Traders.Values
                .Select(t => dbContext.Traders.Local.FirstOrDefault(tr => tr.TelegramId == t.Id))
                .Where(tr => tr != null)
                .Select(tr => tr!)
                .ToList();

            await taskDispatcher.SendTaskAsync("notification",
                NotificationEvent.FromOrderList(ordersToNotify, traders, logger));
        }
    }
}
