using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderCancelService : IOrderCancelService
    {
        readonly IUnitOfWork _unitOfWork;

        public OrderCancelService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CancelOrderResult> CancelOrderAsync(long traderId, string orderId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    var order = await _unitOfWork.Orders.GetByIdAsync(orderId);

                    if (order == null)
                        return new CancelOrderResult(false, "Ордера не существует");

                    order.Cancel(traderId);

                    await _unitOfWork.Orders.UpdateAsync(order);

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
            });
        }

        public async Task<CancelOrderResult> CancelAllOrderAsync(long traderId)
        {
            try
            {
                var orders = await _unitOfWork.Orders.GetPendingByTraderAsync(traderId);

                if (orders == null || orders.Length == 0)
                    return new CancelOrderResult(false, "Нет активных одеров для отмены");

                foreach (var order in orders)
                    order.Cancel(traderId);

                await _unitOfWork.Orders.UpdateRangeAsync(orders);

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
