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

        public async Task<ValidationResult> ValidateFullOrderAsync(CreateOrderCommand request)
        {
            var priceValidationResult = ValidatePrice(request.Price);
            if (!priceValidationResult.IsValid)
                return ValidationResult.Failed(priceValidationResult.Message);

            var quantityValidationResult = ValidateQuantity(request.Quantity);
            if (!quantityValidationResult.IsValid)
                return ValidationResult.Failed(quantityValidationResult.Message);

            var tokenValidationResult = await ValidateTokenAsync(request.TraderId, request.Symbol, request.Direction);
            if (!tokenValidationResult.IsValid)
                return ValidationResult.Failed(tokenValidationResult.Message);

            return ValidationResult.Success();
        }

        public async Task<ValidationResult> ValidateTokensAsync(long traderId, IReadOnlyCollection<string> symbols, string direction)
        {
            if (direction == OrderDirections.Buy)
                return ValidationResult.Success();

            var distinctSymbols = symbols.Distinct().ToList();
            if (distinctSymbols.Count == 0)
                return ValidationResult.Success();

            var ownedSymbols = await dbContext.PortfolioItems
                .Where(p => p.TraderTelegramId == traderId && distinctSymbols.Contains(p.CharacterTokenId))
                .Select(p => p.CharacterTokenId)
                .ToListAsync();

            var ownedSet = ownedSymbols.ToHashSet();
            var missingSymbol = distinctSymbols.FirstOrDefault(s => !ownedSet.Contains(s));

            return missingSymbol == null
                ? ValidationResult.Success()
                : ValidationResult.Failed($"Пользователь не обладает токеном {missingSymbol}");
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

            var sellerGroups = requests
                .Where(r => r.Direction == OrderDirections.Sell)
                .GroupBy(r => (r.TraderId, r.Symbol))
                .Select(g => g.Key)
                .ToList();

            foreach (var (traderId, symbol) in sellerGroups)
            {
                var tokenValidationResult = await ValidateTokensAsync(
                    traderId, new[] { symbol }, OrderDirections.Sell);

                if (!tokenValidationResult.IsValid)
                    return ValidationResult.Failed(tokenValidationResult.Message);
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
