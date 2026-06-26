namespace ArkWallet.Presentation.DTOs
{
    public record GetOrdersRequest(bool IncludeActive, bool IncludeInactive);
    public record GetOrdersResponse(OrderItem[] Orders);
    public record OrderItem(
        string OrderId, string Symbol, string TokenName, 
        string Direction, decimal TotalQuantity, decimal FilledQuantity, 
        decimal FillPercent, decimal OrderPrice, decimal CurrentPrice, string IconUrl);
}
