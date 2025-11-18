namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface IOrderCancelService
    {
        Task<CancelOrderResult> CancelOrderAsync(long traderId, string orderId);
    }

    public record CancelOrderResult(bool IsSuccess, string Message = null);
}
