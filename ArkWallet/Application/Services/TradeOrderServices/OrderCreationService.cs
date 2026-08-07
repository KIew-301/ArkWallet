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
            var context = await PrepareSingleTradingContextAsync(command);
            tradingEngine.ProcessOrder(context);

            var saveResult = await SaveChangesAsync(context);
            if (!saveResult.IsSuccess)
                return Result<OrderCreationData>.Fail(saveResult.Message);

            await NotifyAsync(context);

            var order = context.NewOrders.First();
            return Result<OrderCreationData>.Ok(new(order.IsFilled(), OrderDto.FromEntity(order)));
        }, logger, nameof(OrderCreationService));
    }

    public async Task<Result<List<OrderCreationData>>> CreateOrdersAsync(IEnumerable<CreateOrderCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var commandList = commands.ToList();
            if (!commandList.Any())
                return Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>());

            var groups = commandList
                .GroupBy(c => new { c.Direction, c.Symbol })
                .ToList();

            var allResults = new List<OrderCreationData>();
            var allContexts = new List<TradingContext>();

            foreach (var group in groups)
            {
                var groupCommands = group.ToList();
                var context = await PrepareGroupTradingContextAsync(groupCommands);
                tradingEngine.ProcessOrders(context);
                allContexts.Add(context);

                foreach (var order in context.NewOrders)
                {
                    allResults.Add(new(order.IsFilled(), OrderDto.FromEntity(order)));
                }
            }

            foreach (var context in allContexts)
            {
                var saveResult = await SaveChangesAsync(context);
                if (!saveResult.IsSuccess)
                    return Result<List<OrderCreationData>>.Fail(saveResult.Message);

                await NotifyAsync(context);
            }

            return Result<List<OrderCreationData>>.Ok(allResults);
        }, logger, nameof(OrderCreationService));
    }

    private async Task<TradingContext> PrepareSingleTradingContextAsync(CreateOrderCommand command)
    {
        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == command.TraderId);
        var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == command.Symbol);

        if (trader == null)
            throw new InvalidOperationException("Пользователя не существует");

        if (token == null)
            throw new InvalidOperationException("Токена не существует");

        var validationResult = await orderValidationService.ValidateFullOrderAsync(command);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(validationResult.Message);

        var orderType = command.Direction.Equals("купить", StringComparison.CurrentCultureIgnoreCase)
            ? OrderType.Buy
            : OrderType.Sell;

        var order = TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity);

        if (order.IsLong() && trader.Balance < order.GetReservedBalance())
            throw new InvalidOperationException("Недостаточно средств для выставления ордера");

        var existingOrders = await GetActiveOrdersForMatchingAsync(order);
        var traders = await GetTradersForOrderAsync(existingOrders, order.TraderTelegramId);

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
            ExistingOrders = existingOrders.ToList(),
            Traders = traders,
            Portfolios = portfolios,
            Token = token,
            AllTrades = new List<Trade>()
        };
    }

    private async Task<TradingContext> PrepareGroupTradingContextAsync(IEnumerable<CreateOrderCommand> commands)
    {
        var commandList = commands.ToList();
        if (!commandList.Any())
            throw new InvalidOperationException("Нет команд для обработки");

        var firstCommand = commandList.First();
        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == firstCommand.TraderId)
            ?? throw new InvalidOperationException("Пользователя не существует");

        var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == firstCommand.Symbol)
            ?? throw new InvalidOperationException("Токена не существует");

        var orders = new List<TradeOrder>();
        foreach (var command in commandList)
        {
            var validationResult = await orderValidationService.ValidateFullOrderAsync(command);
            if (!validationResult.IsValid)
                throw new InvalidOperationException(validationResult.Message);

            var orderType = command.Direction.Equals("купить", StringComparison.CurrentCultureIgnoreCase)
                ? OrderType.Buy
                : OrderType.Sell;

            var order = TradeOrder.Create(orderType, command.Symbol, command.TraderId, command.Price, command.Quantity);
            orders.Add(order);
        }

        var isBuy = orders.First().IsLong();

        var targetOrder = isBuy
            ? orders.OrderByDescending(o => o.Price).First()
            : orders.OrderBy(o => o.Price).First();

        if (isBuy)
        {
            var totalReserved = orders.Sum(o => o.GetReservedBalance());
            if (trader.Balance < totalReserved)
                throw new InvalidOperationException("Недостаточно средств для выставления ордеров");
        }

        var existingOrders = await GetActiveOrdersForMatchingAsync(targetOrder);
        var traders = await GetTradersForOrderAsync(existingOrders, trader.TelegramId);
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
            ExistingOrders = existingOrders.ToList(),
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

    private async Task<Dictionary<long, Trader>> GetTradersForOrderAsync(TradeOrder[] activeOrders, long newOrderTraderId)
    {
        var traderIds = activeOrders.Select(o => o.TraderTelegramId).Append(newOrderTraderId).Distinct().ToHashSet();
        var traders = await dbContext.Traders.Where(t => traderIds.Contains(t.TelegramId)).ToArrayAsync();
        return traders.ToDictionary(t => t.TelegramId);
    }

    private async Task<Dictionary<long, PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
    {
        var portfolios = await dbContext.PortfolioItems
            .Where(p => traderIds.Contains(p.TraderTelegramId) && p.CharacterTokenId == symbol)
            .ToArrayAsync();
        return portfolios.ToDictionary(p => p.TraderTelegramId);
    }

    private async Task<Result> SaveChangesAsync(TradingContext context)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (context.NewOrdersToAdd.Any())
                await dbContext.TradeOrders.AddRangeAsync(context.NewOrdersToAdd);

            var modifiedOrders = context.ModifiedOrders
                .Where(o => !context.NewOrdersToAdd.Contains(o))
                .ToList();

            if (modifiedOrders.Any())
                dbContext.TradeOrders.UpdateRange(modifiedOrders);

            if (context.NewTradesToAdd.Any())
                await dbContext.Trades.AddRangeAsync(context.NewTradesToAdd);

            if (context.ModifiedTraders.Any())
                dbContext.Traders.UpdateRange(context.ModifiedTraders);

            if (context.NewPortfoliosToAdd.Any())
                await dbContext.PortfolioItems.AddRangeAsync(context.NewPortfoliosToAdd);

            var modifiedPortfolios = context.ModifiedPortfolios
                .Where(p => !context.NewPortfoliosToAdd.Contains(p))
                .ToList();

            if (modifiedPortfolios.Any())
                dbContext.PortfolioItems.UpdateRange(modifiedPortfolios);

            if (context.ModifiedTokens.Any())
                dbContext.CharacterTokens.UpdateRange(context.ModifiedTokens);

            if (context.AllTrades.Any())
            {
                var result = await tokenPriceCandleUpdateService
                    .UpdateTokenPriceCandleAsync(context.Token.Symbol, context.AllTrades.Last().Price);

                if (!result.IsSuccess)
                    return Result.Fail(result.Message);
            }

            await dbContext.SaveChangesAsync();
            return Result.Ok();
        }, logger, nameof(OrderCreationService));
    }

    private async Task NotifyAsync(TradingContext context)
    {
        var ordersToNotify = context.ExistingOrders.Where(o => o.Status == OrderStatus.Filled).ToList();
        var filledOrders = context.NewOrders.Where(o => o.IsFilled()).ToList();
        ordersToNotify.AddRange(filledOrders);

        if (ordersToNotify.Any())
        {
            await taskDispatcher.SendTaskAsync("notification",
                NotificationEvent.FromOrderList(ordersToNotify, context.Traders.Values.ToList(), logger));
        }
    }
}