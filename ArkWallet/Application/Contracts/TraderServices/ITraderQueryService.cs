using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TraderServices
{
    public interface ITraderQueryService
    {
        Task<TraderInfoDto?> GetTraderInfoAsync(long traderId);
        Task<decimal> GetTraderBalanceAsync(long traderId);
        Task<decimal> GetTraderAvailableBalanceAsync(long traderId);
    }
}
