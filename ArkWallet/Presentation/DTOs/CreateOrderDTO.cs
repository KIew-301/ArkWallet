namespace ArkWallet.Presentation.DTOs
{
    public record CreateOrderRequest(string Symbol, decimal Price, int Quantity, string Direction);
    public record CreateOrderResponse(string OrderId, bool IsFilled);
}