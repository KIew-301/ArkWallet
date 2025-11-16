using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    public interface IPortfolioQueryService
    {
        Task<TokenBalanceDto?> GetTokenBalanceAsync(long traderId, string symbol);
        Task<List<TokenBalanceDto>> GetTraderTokensAsync(long traderId);
        Task<TokenBalanceDto?> GetAvailableTokenBalanceAsync(long traderId, string symbol);
        Task<List<TokenBalanceDto>> GetAvailableTraderTokensAsync(long traderId);
    }
}
