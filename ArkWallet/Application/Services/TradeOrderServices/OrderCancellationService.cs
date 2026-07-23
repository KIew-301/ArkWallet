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
            var order = await dbContext.TradeOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

            if (trader == null)
                return Fail("Трейдер не найден");

            if (order == null)
                return Fail("Ордера не существует");

            if (!order.IsActive())
                return Fail("Можно отменить только активный ордер");

            order.Cancel(traderId);

            if (order.IsLong())
            {
                trader.AddToBalance(order.GetReservedBalance());
            }
            else
            {
                var portfolioItem = await dbContext.PortfolioItems
                    .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == order.CharacterTokenId);
                portfolioItem.ReturnTokens(order.GetRemainingQuantity());
            }

            dbContext.TradeOrders.Update(order);
            await dbContext.SaveChangesAsync();

            return Ok();
        }, logger, nameof(OrderCancellationService));
    }

    public async Task<Result<int>> CancelAllOrderAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

            if (trader == null)
                return Result<int>.Fail("Трейдер не найден");

            var orders = await dbContext.TradeOrders
                .Where(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active)
                .ToArrayAsync();

            if (orders.Length == 0)
                return Result<int>.Fail("Нет активных ордеров для отмены");

            foreach (var order in orders)
            {
                await CancelAndRestoreOrderResources(order, traderId, trader);
            }

            dbContext.TradeOrders.UpdateRange(orders);
            await dbContext.SaveChangesAsync();

            return Result<int>.Ok(orders.Length);
        }, logger, nameof(OrderCancellationService));
    }

    public async Task<bool> HasActiveOrdersAsync(long traderId)
    {
        return await dbContext.TradeOrders
            .AnyAsync(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active);
    }

    private async Task CancelAndRestoreOrderResources(TradeOrder order, long traderId, Trader trader)
    {
        order.Cancel(traderId);

        if (order.IsLong())
        {
            trader.AddToBalance(order.GetReservedBalance());
        }
        else
        {
            var portfolioItem = await dbContext.PortfolioItems
                .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == order.CharacterTokenId);
            if (portfolioItem != null)
                portfolioItem.ReturnTokens(order.GetRemainingQuantity());
        }
    }
}
