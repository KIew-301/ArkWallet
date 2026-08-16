using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderValidationService(ArkWalletDbContext dbContext) : IOrderValidationService
    {
        public ValidationResult ValidateDirection(string direction)
        {
            if (string.IsNullOrEmpty(direction))
                return new ValidationResult(false, "Некорректный ответ - пустая строка");
            if (NormalizeDirection(direction) == null)
                return new ValidationResult(false, "Необходимо выбрать КУПИТЬ или ПРОДАТЬ");

            return new ValidationResult(true);
        }

        public static string? NormalizeDirection(string? direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return null;

            var trimmed = direction.Trim();
            if (trimmed.Equals(OrderDirections.Buy, StringComparison.CurrentCultureIgnoreCase))
                return OrderDirections.Buy;
            if (trimmed.Equals(OrderDirections.Sell, StringComparison.CurrentCultureIgnoreCase))
                return OrderDirections.Sell;

            return null;
        }

        public async Task<ValidationResult> ValidateOrderCancellationAsync(long traderId, string orderId)
        {
            var order = await dbContext.TradeOrders.FirstOrDefaultAsync(o => o.Id == orderId);

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

        public async Task<ValidationResult> ValidateFullOrderAsync(CreateOrderCommand request)
        {
            var priceValidationResult = ValidatePrice(request.Price);
            if (!priceValidationResult.IsValid)
                return ValidationResult.Failed(priceValidationResult.Message);

            var quantityValidationResult = ValidateQuantity(request.Quantity);
            if (!quantityValidationResult.IsValid)
                return ValidationResult.Failed(quantityValidationResult.Message);

            return ValidationResult.Success();
        }

        public async Task<ValidationResult> ValidateFullOrdersAsync(IReadOnlyCollection<CreateOrderCommand> requests)
        {
            if (requests.Count == 0)
                return ValidationResult.Success();

            foreach (var request in requests)
            {
                var priceValidationResult = ValidatePrice(request.Price);
                if (!priceValidationResult.IsValid)
                    return ValidationResult.Failed(priceValidationResult.Message);

                var quantityValidationResult = ValidateQuantity(request.Quantity);
                if (!quantityValidationResult.IsValid)
                    return ValidationResult.Failed(quantityValidationResult.Message);
            }

            return ValidationResult.Success();
        }
    }

    public static class OrderDirections
    {
        public const string Buy = "купить";
        public const string Sell = "продать";
    }
}
