using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    public interface ICancelOrderService
    {
        Task<CancelOrderResult> CancelOrderAsync(long traderId, string orderId);
    }

    public record CancelOrderResult(bool IsSuccess, string Message = null);
}
