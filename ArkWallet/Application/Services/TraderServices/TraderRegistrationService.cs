using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.TraderServices
{
    internal class TraderRegistrationService : ITraderRegistrationService
    {
        readonly IUnitOfWork _unitOfWork;

        public TraderRegistrationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<RegistrationResult> RegisterTraderAsync(long telegramId, string name)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(name))
                    return new RegistrationResult(false, "Имя не может быть пустым");

                if (telegramId <= 0)
                    return new RegistrationResult(false, "Некорректный ID пользователя");

                var trader = await _unitOfWork.Traders.GetByIdAsync(telegramId);

                if (trader != null)
                    return new RegistrationResult(false, "Пользователь уже существует");

                trader = Trader.Create(telegramId, name);

                await _unitOfWork.Traders.AddAsync(trader);

                return new RegistrationResult(true);
            });
        }
    }
}
