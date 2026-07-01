using ArkWallet.Application.Contracts.TradeOrderServices;

namespace ArkWallet.Presentation.DTOs
{
    public record GetOrdersRequest(bool IncludeActive, bool IncludeFilled, bool IncludeCancelled);
    public record GetOrdersResponse(OrderInfo[] Orders);
}
