using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderCancelService(ArkWalletDbContext dbContext) : IOrderCancelService
    {
        public async Task<CancelOrderResult> CancelOrderAsync(long traderId, string orderId)
        {
            try
            {
                var order = await dbContext.TradeOrders.FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                    return new CancelOrderResult(false, "Ордера не существует");

                order.Cancel(traderId);

                dbContext.TradeOrders.Update(order);

                return new CancelOrderResult(true);
            }
            catch (DomainException ex)
            {
                return new CancelOrderResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                return new CancelOrderResult(false, "Ошибка системы");
            }
        }

        public async Task<CancelOrderResult> CancelAllOrderAsync(long traderId)
        {
            try
            {
                var orders = await dbContext.TradeOrders
                    .Where(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active)
                    .ToArrayAsync();

                if (orders == null || orders.Length == 0)
                    return new CancelOrderResult(false, "Нет активных одеров для отмены");

                foreach (var order in orders)
                    order.Cancel(traderId);

                dbContext.TradeOrders.UpdateRange(orders);

                return new CancelOrderResult(true, $"Успешно отменённых ордеров: {orders.Length}");
            }
            catch (DomainException ex)
            {
                return new CancelOrderResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                return new CancelOrderResult(false, "Ошибка системы");
            }
        }
    }
}
