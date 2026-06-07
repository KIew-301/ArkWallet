using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Services.TraderServices
{
    internal class TraderQueryService : ITraderQueryService
    {
        readonly IUnitOfWork _unitOfWork;

        public TraderQueryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<decimal> GetTraderBalanceAsync(long traderId)
        {
            var trader = await _unitOfWork.Traders.GetByIdAsync(traderId);
            return trader?.Balance ?? 0;
        }

        public async Task<TraderInfoDto?> GetTraderInfoAsync(long traderId)
        {
            var trader = await _unitOfWork.Traders.GetByIdAsync(traderId);
            return new TraderInfoDto(traderId, trader.Username, trader.Balance);
        }

        public async Task<decimal> GetTraderAvailableBalanceAsync(long traderId)
        {
            var trader = await _unitOfWork.Traders.GetByIdAsync(traderId);
            var reserve = await _unitOfWork.Orders.GetReservedBalanceAsync(traderId);
            return trader?.Balance - reserve ?? 0;
        }
    }
}
