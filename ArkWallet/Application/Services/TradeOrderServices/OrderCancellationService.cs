using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.TradeOrderServices;
using static Result;

internal class OrderCancellationService(ArkWalletDbContext dbContext, ILogger<OrderCancellationService> logger) : IOrderCancellationService
{
    public async Task<Result> CancelOrderAsync(long traderId, string orderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);

                var order = await dbContext.TradeOrders.FirstOrDefaultAsync(o => o.Id == orderId);
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

                if (trader == null)
                    return Fail("Трейдер не найден");

                if (order == null)
                    return Fail("Ордера не существует");

                if (!order.IsActive())
                    return Fail("Можно отменить только активный ордер");

                order.Cancel(traderId);
                RefundSingleOrder(trader, order, traderId);

                dbContext.TradeOrders.Update(order);
                await dbContext.SaveChangesAsync();

                if (BotFilter.IsBot(traderId))
                {
                    dbContext.TradeOrders.Remove(order);
                    await dbContext.SaveChangesAsync();
                }

                return Ok();
            });
        }, logger, nameof(OrderCancellationService));
    }

    public async Task<Result<int>> CancelAllOrderAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([traderId]);

                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

                if (trader == null)
                    return Result<int>.Fail("Трейдер не найден");

                var orders = await dbContext.TradeOrders
                    .Where(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active)
                    .ToArrayAsync();

                if (orders.Length == 0)
                    return Result<int>.Fail("Нет активных ордеров для отмены");

                var portfolioItems = await LoadShortPortfolioItems(traderId, orders);
                RefundOrders(trader, orders, portfolioItems);

                await PersistCancellation(traderId, orders);

                return Result<int>.Ok(orders.Length);
            });
        }, logger, nameof(OrderCancellationService));
    }

    public async Task<bool> HasActiveOrdersAsync(long traderId)
    {
        return await dbContext.TradeOrders
            .AnyAsync(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active);
    }

    private async Task<Dictionary<string, PortfolioItem>> LoadShortPortfolioItems(long traderId, TradeOrder[] orders)
    {
        var shortTokens = orders
            .Where(o => o.IsShort())
            .Select(o => o.CharacterTokenId)
            .Distinct()
            .ToArray();

        if (shortTokens.Length == 0)
            return new Dictionary<string, PortfolioItem>();

        return (await dbContext.PortfolioItems
                .Where(p => p.TraderTelegramId == traderId && shortTokens.Contains(p.CharacterTokenId))
                .ToArrayAsync())
            .GroupBy(p => p.CharacterTokenId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private async Task PersistCancellation(long traderId, TradeOrder[] orders)
    {
        if (BotFilter.IsBot(traderId))
        {
            dbContext.TradeOrders.RemoveRange(orders);
        }
        else
        {
            await dbContext.TradeOrders
                .Where(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active)
                .ExecuteUpdateAsync(o => o.SetProperty(o => o.Status, OrderStatus.Cancelled));
        }

        await dbContext.SaveChangesAsync();
    }

    private void RefundSingleOrder(Trader trader, TradeOrder order, long traderId)
    {
        if (order.IsLong())
        {
            trader.AddToBalance(order.GetReservedBalance());
        }
        else
        {
            var portfolioItem = dbContext.PortfolioItems
                .FirstOrDefault(p => p.TraderTelegramId == traderId && p.CharacterTokenId == order.CharacterTokenId);
            portfolioItem.ReturnTokens(order.GetRemainingQuantity());
        }
    }

    private static void RefundOrders(
        Trader trader,
        IEnumerable<TradeOrder> orders,
        Dictionary<string, PortfolioItem> portfolioItems)
    {
        foreach (var order in orders)
        {
            if (order.IsLong())
            {
                trader.AddToBalance(order.GetReservedBalance());
            }
            else if (portfolioItems.TryGetValue(order.CharacterTokenId, out var portfolioItem))
            {
                portfolioItem.ReturnTokens(order.GetRemainingQuantity());
            }
        }
    }
}
