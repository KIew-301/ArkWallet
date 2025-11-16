using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface IOrderValidationService
    {
        ValidationResult ValidateDirection(string direction);
        Task<ValidationResult> ValidateTokenAsync(long traderId, string symbol, string direction);
        ValidationResult ValidateQuantity(int quantity);
        Task<ValidationResult> ValidateOrderCancellationAsync(long traderId, string orderId);
        Task<ValidationResult> ValidateOrderCreationAsync(long traderId, string symbol, string direction, int quantity, decimal price);
        ValidationResult ValidatePrice(decimal price);
    }

    public record PriceValidationResult(
        bool IsValid,
        string? Message = null,
        decimal? SuggestedPrice = null
    );
}
