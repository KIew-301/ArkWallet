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
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);

                if (order == null)
                    return new CancelOrderResult(false, "Ордера не существует");

                if (trader == null)
                    return new CancelOrderResult(false, "Трейдер не найден");

                if (!order.IsActive())
                    return new CancelOrderResult(false, "Можно отменить только активный ордер");

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
