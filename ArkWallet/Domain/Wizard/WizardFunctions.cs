using ArkWallet.Entities;
using ArkWallet.Repositories;

namespace ArkWallet.Domain.Wizard
{
    internal partial class WizardEngine
    {
        public async Task AddNewTrader(long id, string name)
        {
            var newTrader = new Trader()
            {
                Balance = 500,
                TelegramId = id,
                Username = name
            };

            await _traderRepo.AddAsync(newTrader);
        }
    }
}
