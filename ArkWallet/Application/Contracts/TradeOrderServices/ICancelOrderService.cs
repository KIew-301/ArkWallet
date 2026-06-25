using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    internal interface IOrderCancelService
    {
        Task<Result> CancelOrderAsync(long traderId, string orderId);
        Task<Result> CancelAllOrderAsync(long traderId);
    }
}
