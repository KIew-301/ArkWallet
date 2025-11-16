using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Dtos;
using Microsoft.CodeAnalysis;

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

            return [..items.Select(TokenBalanceDto.FromEntity)];
        }

        public async Task<TokenBalanceDto?> GetAvailableTokenBalanceAsync(long traderId, string symbol)
        {
            var item = await _unitOfWork.Portfolios.GetByTraderAndSymbolAsync(traderId, symbol);
            var reserve = await _unitOfWork.Orders.GetReservedQuantityAsync(traderId, symbol);

            return TokenBalanceDto.FromEntity(item, reserve);
        }

        public async Task<List<TokenBalanceDto>> GetAvailableTraderTokensAsync(long traderId)
        {
            var items = await _unitOfWork.Portfolios.GetByTraderAsync(traderId);
            var reserve = await _unitOfWork.Orders.GetReservedQuantitiesAllAsync(traderId);

            if (items.Count == 0)
                return [];

            return [.. items.Select(i => TokenBalanceDto.FromEntity(i, reserve.GetValueOrDefault(i.CharacterTokenId, 0)))];
        }
    }
}
