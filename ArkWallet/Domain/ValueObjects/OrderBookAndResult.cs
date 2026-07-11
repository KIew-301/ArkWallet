using ArkWallet.Domain.Entities;

namespace ArkWallet.Domain.ValueObjects
{
    internal class OrderBook
    {
        public List<TradeOrder> Bids { get; set; } = new(); // Заявки на покупку
        public List<TradeOrder> Asks { get; set; } = new(); // Заявки на продажу

        public void LoadOrders(List<TradeOrder> orders, long excludeTraderId)
        {
            foreach (var order in orders)
            {
                if (order.TraderTelegramId != excludeTraderId && order.Status == OrderStatus.Active)
                {
                    if (order.Type == OrderType.Buy)
                        Bids.Add(order);
                    else
                        Asks.Add(order);
                }
            }
        }
    }


}
