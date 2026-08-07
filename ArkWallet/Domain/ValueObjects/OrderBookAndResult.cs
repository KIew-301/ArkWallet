using ArkWallet.Domain.Entities;

namespace ArkWallet.Domain.ValueObjects;

internal class OrderBook
{
    public List<TradeOrder> Bids { get; set; } = new();
    public List<TradeOrder> Asks { get; set; } = new();

    public void LoadOrders(List<TradeOrder> orders, long excludeTraderId)
    {
        Bids.Clear();
        Asks.Clear();

        if (orders == null) return;

        foreach (var order in orders)
        {
            if (order == null) continue;

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