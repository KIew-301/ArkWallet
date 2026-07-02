using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TradeOrderServices;
using static ArkWallet.Application.Common.Result<OrderCreationData>;

internal class OrderCreationService(
    ArkWalletDbContext dbContext, TradingEngine tradingEngine,
    IOrderCreationFullValidationService orderCreationFullValidationService,
    ITokenPriceCandleUpdateService tokenPriceCandleUpdateService,
    ITaskDispatcher taskDispatcher) : IOrderCreationService
{
    public async Task<Result<OrderCreationData>> CreateOrderAsync(CreateOrderCommand command)
    {
        try
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == command.TraderId);
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == command.Symbol);

            if (trader == null)
                return Fail("Пользователя не существует");

            if (token == null)
                return Fail("Токена не существует");

            var orderValidationResult = await orderCreationFullValidationService.ValidateAsync(command);

            if (!orderValidationResult.IsValid)
                return Fail(orderValidationResult.Message);

            var orderType = command.Direction.Equals("купить", StringComparison.CurrentCultureIgnoreCase)
                ? OrderType.Buy
                : OrderType.Sell;

            var order = TradeOrder.Create(
                orderType,
                command.Symbol,
                command.TraderId,
                command.Price,
                command.Quantity
            );

            if (order.IsLong())
            {
                if (trader.Balance < order.GetReservedBalance())
                    return Fail("Недостаточно средств для выставления ордера");
            }

            var existingOrders = await GetActiveOrdersForMatchingAsync(order.CharacterTokenId);
            var traders = await GetTradersForOrderAsync(existingOrders, order.TraderTelegramId);
            var portfolios = await GetPortfoliosForTradersAsync(order.CharacterTokenId, traders.Keys);

            if (order.IsShort())
            {
                if (!portfolios.TryGetValue(trader.TelegramId, out var portfolio) || portfolio.Quantity < order.Quantity)
                    return Fail("Недостаточно токенов для создания ордера");
            }

            var engineResult = tradingEngine.ProcessOrder(order, existingOrders.ToList(), traders, portfolios, token);

            if (!engineResult.IsSuccess)
                return Fail("Не удалось выставить ордер");

            if (engineResult.Trades.Count > 0)
            {
                var tokenPriceUpdateResult = await tokenPriceCandleUpdateService
                    .UpdateTokenPriceCandleAsync(command.Symbol, engineResult.Trades.Last().Price);

                if (!tokenPriceUpdateResult.IsSuccess)
                    return Fail(tokenPriceUpdateResult.Message);
            }

            await SaveTradingResultAsync(engineResult);

            string status = order.IsFilled() ? "Исполнен" : "Активен";

            var result = OrderDto.FromEntity(engineResult.OrderToAdd);

            await taskDispatcher.SendTaskAsync("notification", NotificationEvent.FromOrderList(engineResult.UpdatedOrders));

            return Ok(new(order.IsFilled(), result));
        }
        catch (DomainException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private async Task<TradeOrder[]> GetActiveOrdersForMatchingAsync(string symbol)
    {
        return await dbContext.TradeOrders
            .Where(o => o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
            .ToArrayAsync();
    }

    private async Task<Dictionary<long, Trader>> GetTradersForOrderAsync(TradeOrder[] activeOrders, long newOrderTraderId)
    {
        var traderIds = activeOrders
            .Select(o => o.TraderTelegramId)
            .Append(newOrderTraderId)
            .Distinct()
            .ToHashSet();

        var traders = await dbContext.Traders.Where(t => traderIds.Contains(t.TelegramId)).ToArrayAsync();
        return traders.ToDictionary(t => t.TelegramId);
    }

    private async Task<Dictionary<long, PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
    {
        var portfolios = await dbContext.PortfolioItems.Where(p => traderIds.Contains(p.TraderTelegramId) && p.CharacterTokenId == symbol).ToArrayAsync();
        return portfolios.ToDictionary(p => p.TraderTelegramId);
    }

    private async Task SaveTradingResultAsync(TradingResult result)
    {
        if (result.Trades.Any())
            await dbContext.Trades.AddRangeAsync(result.Trades);

        if (result.UpdatedOrders.Any())
            dbContext.TradeOrders.UpdateRange(result.UpdatedOrders);

        if (result.UpdatedTraders.Any())
            dbContext.Traders.UpdateRange(result.UpdatedTraders);

        if (result.UpdatedPortfolios.Any())
            dbContext.PortfolioItems.UpdateRange(result.UpdatedPortfolios);

        if (result.UpdatedToken != null)
            dbContext.CharacterTokens.Update(result.UpdatedToken);

        if (result.OrderToAdd != null)
            await dbContext.TradeOrders.AddAsync(result.OrderToAdd);

        if (result.PortfoliosToAdd.Any())
            await dbContext.PortfolioItems.AddRangeAsync(result.PortfoliosToAdd);

        await dbContext.SaveChangesAsync();
    }
}
