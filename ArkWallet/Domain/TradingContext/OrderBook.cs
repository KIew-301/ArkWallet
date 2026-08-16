namespace ArkWallet.Domain.TradingContext;

internal class OrderBook
{
    public List<Order> Bids { get; private set; } = new();
    public List<Order> Asks { get; private set; } = new();

    public void LoadOrders(IEnumerable<Order> orders, long excludeTraderId)
    {
        Bids.Clear();
        Asks.Clear();

        if (orders == null)
            return;

        foreach (var order in orders)
        {
            if (order == null)
                continue;

            if (order.TraderId == excludeTraderId || !order.IsActive())
                continue;

            if (order.IsLong())
                Bids.Add(order);
            else
                Asks.Add(order);
        }
    }
}
