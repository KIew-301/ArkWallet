using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    internal interface IOrderCreationService
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

    internal record OrderCreationResult(
        bool IsSuccess,
        bool IsFilled,
        OrderDto? Order = null,
        string? ErrorMessage = null,
        List<OrderDto?>? ClosesOrder = null
    );
}
