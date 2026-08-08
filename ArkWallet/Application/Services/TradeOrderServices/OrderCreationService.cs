using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TradeOrderServices;

internal class OrderCreationService(
    ArkWalletDbContext dbContext,
    TradingEngine tradingEngine,
    IOrderValidationService orderValidationService,
    ITokenPriceCandleUpdateService tokenPriceCandleUpdateService,
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
                tradingEngine.ProcessOrder(context);

                var saveResult = await SaveChangesAsync(context);
                if (!saveResult.IsSuccess)
                    return Result<OrderCreationData>.Fail(saveResult.Message);

                await NotifyAsync(context);

                var order = context.NewOrders[0];
                return Result<OrderCreationData>.Ok(new(order.IsFilled(), OrderDto.FromEntity(order)));
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
                    var saveResult = await SaveChangesAsync(context);
                    if (!saveResult.IsSuccess)
                        return Result<List<OrderCreationData>>.Fail(saveResult.Message);

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
        tradingEngine.ProcessOrders(context);
        allContexts.Add(context);

        foreach (var order in context.NewOrders)
        {
            allResults.Add(new(order.IsFilled(), OrderDto.FromEntity(order)));
        }
    }

    private async Task<TradingContext> PrepareSingleTradingContextAsync(CreateOrderCommand command)
    {
        var orderType = command.Direction.Equals("купить", StringComparison.CurrentCultureIgnoreCase)
            ? OrderType.Buy
            : OrderType.Sell;

        var order = TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity);

        var takerIds = await GetTakerIdsForMatchingAsync(order);

        await dbContext.LockTradersAsync(takerIds.Append(order.TraderTelegramId));
        await dbContext.LockTokenAsync(order.CharacterTokenId);

        var token = await dbContext.CharacterTokens.FindAsync(command.Symbol);

        if (token == null)
            throw new InvalidOperationException("Токена не существует");

        var activeOrders = await GetActiveOrdersForMatchingAsync(order);

        var trader = await dbContext.Traders.FindAsync(command.TraderId);

        if (trader == null)
            throw new InvalidOperationException("Пользователя не существует");

        var validationResult = await orderValidationService.ValidateFullOrderAsync(command);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(validationResult.Message);

        if (order.IsLong() && trader.Balance < order.GetReservedBalance())
            throw new InvalidOperationException("Недостаточно средств для выставления ордера");

        var traders = await GetTradersForOrderAsync(activeOrders, order.TraderTelegramId);

        var traderIds = traders.Keys.ToList();
        if (!traderIds.Contains(order.TraderTelegramId))
            traderIds.Add(order.TraderTelegramId);

        var portfolios = await GetPortfoliosForTradersAsync(order.CharacterTokenId, traderIds);

        if (order.IsShort())
        {
            if (!portfolios.TryGetValue(trader.TelegramId, out var portfolio))
                throw new InvalidOperationException("У вас нет токенов для продажи");

            if (portfolio.Quantity < order.Quantity)
                throw new InvalidOperationException($"Недостаточно токенов. Доступно: {portfolio.Quantity}, нужно: {order.Quantity}");
        }

        return new TradingContext
        {
            NewOrders = new List<TradeOrder> { order },
            ExistingOrders = activeOrders.ToList(),
            Traders = traders,
            Portfolios = portfolios,
            Token = token,
            AllTrades = new List<Trade>()
        };
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

        var orders = new List<TradeOrder>();
        foreach (var command in commandList)
        {
            var orderType = command.Direction.Equals("купить", StringComparison.CurrentCultureIgnoreCase)
                ? OrderType.Buy
                : OrderType.Sell;

            orders.Add(TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity));
        }

        var isBuy = orders[0].IsLong();

        var targetOrder = isBuy
            ? orders.OrderByDescending(o => o.Price).First()
            : orders.OrderBy(o => o.Price).First();

        var takerIds = await GetTakerIdsForMatchingAsync(targetOrder);

        await dbContext.LockTradersAsync(takerIds.Append(firstCommand.TraderId));
        await dbContext.LockTokenAsync(targetOrder.CharacterTokenId);

        var token = await dbContext.CharacterTokens.FindAsync(firstCommand.Symbol)
            ?? throw new InvalidOperationException("Токена не существует");

        var activeOrders = await GetActiveOrdersForMatchingAsync(targetOrder);

        var trader = await dbContext.Traders.FindAsync(firstCommand.TraderId)
            ?? throw new InvalidOperationException("Пользователя не существует");

        if (isBuy)
        {
            var totalReserved = orders.Sum(o => o.GetReservedBalance());
            if (trader.Balance < totalReserved)
                throw new InvalidOperationException("Недостаточно средств для выставления ордеров");
        }

        var traders = await GetTradersForOrderAsync(activeOrders, trader.TelegramId);
        var portfolios = await GetPortfoliosForTradersAsync(targetOrder.CharacterTokenId, traders.Keys);

        if (!isBuy)
        {
            var totalQuantity = orders.Sum(o => o.Quantity);
            if (!portfolios.TryGetValue(trader.TelegramId, out var portfolio) || portfolio.Quantity < totalQuantity)
                throw new InvalidOperationException("Недостаточно токенов для создания ордеров");
        }

        var sortedOrders = isBuy
            ? orders.OrderBy(o => o.Price).ToList()
            : orders.OrderByDescending(o => o.Price).ToList();

        return new TradingContext
        {
            NewOrders = sortedOrders,
            ExistingOrders = activeOrders.ToList(),
            Traders = traders,
            Portfolios = portfolios,
            Token = token,
            AllTrades = new List<Trade>()
        };
    }

    private async Task<TradeOrder[]> GetActiveOrdersForMatchingAsync(TradeOrder order)
    {
        return order.IsLong()
            ? await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == OrderStatus.Active &&
                           o.Type == OrderType.Sell &&
                           o.Price <= order.Price)
                .ToArrayAsync()
            : await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == OrderStatus.Active &&
                           o.Type == OrderType.Buy &&
                           o.Price >= order.Price)
                .ToArrayAsync();
    }

    private async Task<long[]> GetTakerIdsForMatchingAsync(TradeOrder order)
    {
        return order.IsLong()
            ? await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == OrderStatus.Active &&
                           o.Type == OrderType.Sell &&
                           o.Price <= order.Price)
                .Select(o => o.TraderTelegramId)
                .Distinct()
                .ToArrayAsync()
            : await dbContext.TradeOrders
                .Where(o => o.CharacterTokenId == order.CharacterTokenId &&
                           o.Status == OrderStatus.Active &&
                           o.Type == OrderType.Buy &&
                           o.Price >= order.Price)
                .Select(o => o.TraderTelegramId)
                .Distinct()
                .ToArrayAsync();
    }

    private async Task<Dictionary<long, Trader>> GetTradersForOrderAsync(TradeOrder[] activeOrders, long newOrderTraderId)
    {
        var traderIds = activeOrders.Select(o => o.TraderTelegramId).Append(newOrderTraderId).Distinct().ToHashSet();

        var traders = new Dictionary<long, Trader>();
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

    private async Task<Dictionary<long, PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
    {
        var ids = traderIds.Distinct().ToHashSet();

        var portfolios = new Dictionary<long, PortfolioItem>();
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

    private async Task<Result> SaveChangesAsync(TradingContext context)
    {
        var stageResult = await StageChangesAsync(context);
        if (!stageResult.IsSuccess)
            return stageResult;

        await dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    private async Task<Result> StageChangesAsync(TradingContext context)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            StageOrders(dbContext, context);
            await StageTradesAndTradersAsync(dbContext, context);
            await StagePortfoliosAsync(dbContext, context);
            StageTokens(dbContext, context);
            return await UpdateTokenPriceIfNeededAsync(context);
        }, logger, nameof(OrderCreationService));
    }

    private static void StageOrders(ArkWalletDbContext dbContext, TradingContext context)
    {
        if (context.NewOrdersToAdd.Count > 0)
            dbContext.TradeOrders.AddRange(context.NewOrdersToAdd);

        var modifiedOrders = context.ModifiedOrders
            .Where(o => !context.NewOrdersToAdd.Contains(o))
            .ToList();

        if (modifiedOrders.Count > 0)
            dbContext.TradeOrders.UpdateRange(modifiedOrders);
    }

    private static async Task StageTradesAndTradersAsync(ArkWalletDbContext dbContext, TradingContext context)
    {
        if (context.NewTradesToAdd.Count > 0)
            await dbContext.Trades.AddRangeAsync(context.NewTradesToAdd);

        if (context.ModifiedTraders.Count > 0)
            dbContext.Traders.UpdateRange(context.ModifiedTraders);
    }

    private static async Task StagePortfoliosAsync(ArkWalletDbContext dbContext, TradingContext context)
    {
        if (context.NewPortfoliosToAdd.Count > 0)
            await dbContext.PortfolioItems.AddRangeAsync(context.NewPortfoliosToAdd);

        var modifiedPortfolios = context.ModifiedPortfolios
            .Where(p => !context.NewPortfoliosToAdd.Contains(p))
            .ToList();

        if (modifiedPortfolios.Count > 0)
            dbContext.PortfolioItems.UpdateRange(modifiedPortfolios);
    }

    private static void StageTokens(ArkWalletDbContext dbContext, TradingContext context)
    {
        if (context.ModifiedTokens.Count > 0)
            dbContext.CharacterTokens.UpdateRange(context.ModifiedTokens);
    }

    private async Task<Result> UpdateTokenPriceIfNeededAsync(TradingContext context)
    {
        if (context.AllTrades.Count == 0)
            return Result.Ok();

        var result = await tokenPriceCandleUpdateService
            .UpdateTokenPriceCandleAsync(context.Token.Symbol, context.AllTrades[^1].Price);

        return result.IsSuccess ? Result.Ok() : Result.Fail(result.Message);
    }

    private async Task NotifyAsync(TradingContext context)
    {
        var ordersToNotify = context.ExistingOrders.Where(o => o.Status == OrderStatus.Filled).ToList();
        var filledOrders = context.NewOrders.Where(o => o.IsFilled()).ToList();
        ordersToNotify.AddRange(filledOrders);

        if (ordersToNotify.Count > 0)
        {
            await taskDispatcher.SendTaskAsync("notification",
                NotificationEvent.FromOrderList(ordersToNotify, context.Traders.Values.ToList(), logger));
        }
    }
}