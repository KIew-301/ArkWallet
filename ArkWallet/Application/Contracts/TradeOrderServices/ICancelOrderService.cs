using ArkWallet.Application.Common;
using ArkWallet.Application.Services.TraderServices;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    internal interface IOrderCancelService
    {
        Task<Result> CancelOrderAsync(long traderId, string orderId);
        Task<Result> CancelAllOrderAsync(long traderId);
    }
}
