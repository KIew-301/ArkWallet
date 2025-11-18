namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface IOrderCancelService
    {
        Task<CancelOrderResult> CancelOrderAsync(long traderId, string orderId);
        Task<CancelOrderResult> CancelAllOrderAsync(long traderId);
    }

    public record CancelOrderResult(bool IsSuccess, string Message = null);
}
