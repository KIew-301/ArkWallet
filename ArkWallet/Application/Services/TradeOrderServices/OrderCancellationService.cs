using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
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

    public async Task<Result> CancelAllOrderAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var orders = await dbContext.TradeOrders
                .Where(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active)
                .ToArrayAsync();

            if (orders == null || orders.Length == 0)
                return Fail("Нет активных одеров для отмены");

            foreach (var order in orders)
                order.Cancel(traderId);

            await dbContext.SaveChangesAsync();

            return Ok();
        }, logger, nameof(OrderCancellationService));
    }
}
