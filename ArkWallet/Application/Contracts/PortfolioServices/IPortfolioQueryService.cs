using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    public interface IPortfolioQueryService
    {
        Task<List<TokenBalanceDto>> GetTraderTokensAsync(long traderId);
        Task<TokenBalanceDto?> GetTokenBalanceAsync(long traderId, string symbol);
    }
}
