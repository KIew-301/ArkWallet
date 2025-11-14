using ArkWallet.Application.Contracts;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Services
{
    internal class CancelOrderService
    {
        private readonly IUnitOfWork _uow;

        public CancelOrderService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<CancelOrderResult> CancelOrder(long traderId, string orderId)
        {
            return await _uow.ExecuteInTransactionAsync(async () =>
            {
                // 1. Проверяем что ордер принадлежит пользователю
                var order = await _uow.Orders.GetByIdAsync(orderId);
                if (order == null)
                    return CancelOrderResult.Failed("Ордер не найден");

                if (order.TraderTelegramId != traderId)
                    return CancelOrderResult.Failed("Это не ваш ордер");

                // 2. Отменяем ордер
                var success = await _uow.Orders.CancelOrderAsync(orderId);
                if (!success)
                    return CancelOrderResult.Failed("Ордер нельзя отменить");

                return CancelOrderResult.Success(order);
            });
        }
    }

    internal class CancelOrderResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public TradeOrder Order { get; set; }

        public static CancelOrderResult Success(TradeOrder order)
            => new() { IsSuccess = true, Message = "Ордер отменён", Order = order };

        public static CancelOrderResult Failed(string error)
            => new() { IsSuccess = false, Message = error };
    }
}