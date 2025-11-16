using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.PortfolioServices
{
    internal class PortfolioUpdatingService : IPortfolioUpdatingService
    {
        readonly IUnitOfWork _unitOfWork;

        public PortfolioUpdatingService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PortfolioUpdatingResult> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var item = await _unitOfWork.Portfolios.GetByTraderAndSymbolAsync(traderId, symbol);
                var token = await _unitOfWork.Tokens.GetByIdAsync(symbol);

                if (token == null)
                    return new PortfolioUpdatingResult(false, "Токена не существует");

                if (item == null)
                    item = PortfolioItem.Create(traderId, symbol, quantity, token.CurrentPrice);
                else
                    item.AddTokens(quantity, token.CurrentPrice);

                await _unitOfWork.Portfolios.UpdateAsync(item);

                return new PortfolioUpdatingResult(true);
            });
        }
    }
}
