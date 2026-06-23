namespace ArkWallet.Presentation.DTOs
{
    public record CreateOrderRequest(string Symbol, decimal Price, decimal Quantity, string Direction);
    public record CreateOrderResponse(bool IsSuccess, string Message);
}