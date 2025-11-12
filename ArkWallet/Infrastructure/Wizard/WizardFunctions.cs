using ArkWallet.Entities;
using ArkWallet.ValueObjects;

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

        public async Task<OrderResult> AddNewOrder(UserSession session)
        {
            TradeOrder newOrder = new()
            {
                Type = session.Data["set_direction"].ToString().ToLower()
                    == "купить" ? OrderType.Buy : OrderType.Sell,
                CharacterTokenId = session.Data["set_token"].ToString(),
                TraderTelegramId = session.Id,
                Price = decimal.Parse(session.Data["set_price"].ToString()),
                Quantity = int.Parse(session.Data["set_quantity"].ToString()),
            };

            var result = await _tradingEngine.PlaceOrder(newOrder);
            return result;
        }
    }
}
