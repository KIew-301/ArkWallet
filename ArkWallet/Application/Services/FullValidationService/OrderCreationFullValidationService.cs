using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeOrderServices;

namespace ArkWallet.Application.Services.FullValidationService
{
    internal class OrderCreationFullValidationService(IOrderValidationService orderValidationService) : IOrderCreationFullValidationService
    {
        public async Task<ValidationResult> ValidateAsync(CreateOrderCommand request)
        {
            var priceValidationResult = orderValidationService.ValidatePrice(request.Price);
            if (!priceValidationResult.IsValid)
                return ValidationResult.Failed(priceValidationResult.Message);

            var quantityValidationResult = orderValidationService.ValidateQuantity(request.Quantity);
            if (!quantityValidationResult.IsValid)
                return ValidationResult.Failed(quantityValidationResult.Message);

            var tokenValidationResult = await orderValidationService.ValidateTokenAsync(request.TraderId, request.Symbol, request.Direction);
            if (!tokenValidationResult.IsValid)
                return ValidationResult.Failed(tokenValidationResult.Message);

            return ValidationResult.Success();
        }
    }
}
