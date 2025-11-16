using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Services.PortfolioServices
{
    internal class PortfolioQueryService : IPortfolioQueryService
    {
        readonly IUnitOfWork _unitOfWork;

        public PortfolioQueryService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TokenBalanceDto?> GetTokenBalanceAsync(long traderId, string symbol)
        {
            var item = await _unitOfWork.Portfolios.GetByTraderAndSymbolAsync(traderId, symbol);

            return TokenBalanceDto.FromEntity(item);
        }

        public async Task<List<TokenBalanceDto>> GetTraderTokensAsync(long traderId)
        {
            var items = await _unitOfWork.Portfolios.GetByTraderAsync(traderId);

            if (items.Count == 0)
                return [];

            return items.Select(TokenBalanceDto.FromEntity).ToList();
        }
    }
}
