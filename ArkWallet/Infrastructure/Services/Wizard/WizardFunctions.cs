using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities;

namespace ArkWallet.Infrastructure.Wizard
{
    internal partial class WizardEngine
    {
        public async Task AddNewTrader(long id, string name)
        {
            await _uow.ExecuteInTransactionAsync(async () =>
            {
                var newTrader = new Trader()
                {
                    Balance = 500,
                    TelegramId = id,
                    Username = name
                };

                await _uow.Traders.AddAsync(newTrader);
                return true;
            });
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

            var result = await _orderService.PlaceOrder(newOrder);
            return result;
        }
    }
}
