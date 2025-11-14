using ArkWallet.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    public interface IPortfolioQueryService
    {
        Task<List<TokenBalanceDto>> GetTraderTokensAsync(long traderId);
        Task<TokenBalanceDto?> GetTokenBalanceAsync(long traderId, string symbol);
        Task<PortfolioSummaryDto> GetPortfolioSummaryAsync(long traderId);
    }
}
