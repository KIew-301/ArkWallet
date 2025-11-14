using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Services
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

                _unitOfWork.Traders.UpdateBalanceAsync(traderId, amount);
            });
        }
    }
}
