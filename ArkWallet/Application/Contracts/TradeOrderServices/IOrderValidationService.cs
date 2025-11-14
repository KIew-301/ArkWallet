using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface IOrderValidationService
    {
        Task<ValidationResult> ValidateDirectionAsync(string direction);
        Task<ValidationResult> ValidateTokenAsync(long traderId, string symbol, string direction);
        Task<ValidationResult> ValidateQuantityAsync(long traderId, string symbol, string direction, int quantity);
        Task<ValidationResult> ValidateCancelOrderAsync(long traderId, string symbol, string direction, int quantity);
        Task<ValidationResult> ValidateOrderCancellationAsync(long traderId, string orderId);
        Task<PriceValidationResult> ValidatePriceAsync(long traderId, string symbol, string direction, int quantity, decimal price);
    }

    public record PriceValidationResult(
        bool IsValid,
        string? Message = null,
        decimal? SuggestedPrice = null
    );
}
