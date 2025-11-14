using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface IOrderCreationService
    {
        Task<OrderCreationResult> CreateOrderAsync(CreateOrderCommand command);
    }

    public record CreateOrderCommand(
        long TraderId,
        string Direction,
        string Symbol,
        int Quantity,
        decimal Price
    );

    public record OrderCreationResult(
        bool IsSuccess,
        bool IsFilled,
        OrderDto? Order = null,
        string? ErrorMessage = null
    );
}
