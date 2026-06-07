using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderValidationService : IOrderValidationService
    {
        readonly IUnitOfWork _unitOfWork;

        public OrderValidationService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public ValidationResult ValidateDirection(string direction)
        {
            if (string.IsNullOrEmpty(direction))
                return new ValidationResult(false, "Некорректный ответ - пустая строка");
            if (direction != OrderDirections.Buy && direction != OrderDirections.Sell)
                return new ValidationResult(false, "Необходимо выбрать КУПИТЬ или ПРОДАТЬ");

            return new ValidationResult(true);
        }

        public async Task<ValidationResult> ValidateOrderCancellationAsync(long traderId, string orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);

            if (order == null)
                return new ValidationResult(false, "Такого ордера не существует");

            if (!order.IsActive())
                return new ValidationResult(false, "Нельзя отменить неактивный ордер");

            if (!order.IsTraderOrder(traderId))
                return new ValidationResult(false, "Нельзя отменить не своей ордер");

            return new ValidationResult(true);
        }

        public ValidationResult ValidatePrice(decimal price)
        {
            if (price <= 0)
                return new ValidationResult(false, "Цена должна быть больше 0");

            return new ValidationResult(true);
        }

        public ValidationResult ValidateQuantity(int quantity)
        {
            if (quantity <= 0)
                return new ValidationResult(false, "Количество должно быть больше 0");

            return new ValidationResult(true);
        }

        public async Task<ValidationResult> ValidateTokenAsync(long traderId, string symbol, string direction)
        {
            if (direction == OrderDirections.Buy)
                return new ValidationResult(true);

            var item = await _unitOfWork.Portfolios.GetByTraderAndSymbolAsync(traderId, symbol);

            if (item == null)
                return new ValidationResult(false, "Пользователь не обладает данным токеном");

            return new ValidationResult(true);
        }

        public async Task<ValidationResult> ValidateOrderCreationAsync(long traderId, string symbol, string direction, int quantity, decimal price)
        {
            if (direction == OrderDirections.Buy)
            {
                var trader = await _unitOfWork.Traders.GetByIdAsync(traderId);
                var reserve = await _unitOfWork.Orders.GetReservedBalanceAsync(traderId);

                decimal stock = trader.Balance - reserve;
                decimal totalCost = quantity * price;

                if (stock < totalCost)
                    return new ValidationResult(false,
                        $"Не хватает средств (необходимо {totalCost}, доступно {stock})");
            }
            else
            {
                var item = await _unitOfWork.Portfolios.GetByTraderAndSymbolAsync(traderId, symbol);
                var reserve = await _unitOfWork.Orders.GetReservedQuantityAsync(traderId, symbol);

                int stock = item.Quantity - reserve;

                if (stock < quantity)
                    return new ValidationResult(false,
                        $"Не хватает токенов для продажи (необходимо {quantity}, доступно {stock})");
            }

            return new ValidationResult(true);
        }
    }

    public static class OrderDirections
    {
        public const string Buy = "купить";
        public const string Sell = "продать";
    }
}
