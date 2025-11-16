using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class CancelOrderService : ICancelOrderService
    {
        readonly IUnitOfWork _unitOfWork;

        public CancelOrderService(IUnitOfWork unitOfWork)
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
    }
}
