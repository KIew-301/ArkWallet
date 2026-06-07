using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;

namespace ArkWallet.Application.Services.TraderServices
{
    internal class TraderBalanceUpdatingService : ITraderBalanceUpdatingService
    {
        readonly IUnitOfWork _unitOfWork;

        public TraderBalanceUpdatingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TraderBalanceUpdatingResult> AddToBalanceAsync(long traderId, decimal amount)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (amount <= 0)
                    return new TraderBalanceUpdatingResult(false, "Число должно быть больше 0");

                var trader = await _unitOfWork.Traders.GetByIdAsync(traderId);

                if (trader == null)
                    return new TraderBalanceUpdatingResult(false, "Трейдера не существует");

                trader.AddToBalance(amount);

                await _unitOfWork.Traders.UpdateAsync(trader);

                return new TraderBalanceUpdatingResult(true);
            });
        }
    }
}
