using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderValidationService(ArkWalletDbContext dbContext, ReserveCalculationService reserveCalculationService) : IOrderValidationService
    {
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

        public async Task<ValidationResult> ValidateTokenAsync(long traderId, string symbol, string direction)
        {
            if (direction == OrderDirections.Buy)
                return new ValidationResult(true);

            var item = await dbContext.PortfolioItems
                .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);

            if (item == null)
                return new ValidationResult(false, "Пользователь не обладает данным токеном");

            return new ValidationResult(true);
        }

        public async Task<ValidationResult> ValidateOrderCreationAsync(long traderId, string symbol, string direction, int quantity, decimal price)
        {
            if (direction == OrderDirections.Buy)
            {
                var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
                decimal totalCost = quantity * price;

                if (trader.Balance < totalCost)
                    return new ValidationResult(false,
                        $"Не хватает средств (необходимо {totalCost}, доступно {trader.Balance})");
            }
            else
            {
                var item = await dbContext.PortfolioItems
                    .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);

                int stock = item.Quantity;

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
