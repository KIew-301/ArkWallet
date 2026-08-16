using ArkWallet.Domain.Common;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.TradingContext;

namespace ArkWallet.Domain.Engines
{
    internal class TradingEngine(TimeProvider? timeProvider = null)
    {
        public async Task ProcessOrder(TradingContext context)
        {
            if (context.NewOrders == null || context.NewOrders.Count == 0)
                throw new DomainException("Ордер не может быть null");

            var newOrder = context.NewOrders[0];

            await ProcessSingleOrder(newOrder, context);

            await UpdateTokenPrice(context);
        }

        public async Task ProcessOrders(TradingContext context)
        {
            if (context.NewOrders == null || context.NewOrders.Count == 0)
                throw new DomainException("Список ордеров не может быть пустым");

            foreach (var newOrder in context.NewOrders)
                await ProcessSingleOrder(newOrder, context);

            await UpdateTokenPrice(context);
        }

        private async Task ProcessSingleOrder(Order newOrder, TradingContext context)
        {
            if (!context.Traders.TryGetValue(newOrder.TraderId, out _))
                throw new DomainException("Трейдер не найден");

            var orderBook = new OrderBook();
            orderBook.LoadOrders(context.ExistingOrders, newOrder.TraderId);
            context.OrderBook = orderBook;

            var matches = FindMatchingOrders(newOrder, orderBook);
            var remainingQuantity = newOrder.Quantity;

            foreach (var match in matches)
            {
                if (remainingQuantity <= 0)
                    break;

                var tradeQuantity = Math.Min(remainingQuantity, match.GetRemainingQuantity());
                var tradePrice = match.Price;

                var buyOrder = newOrder.IsLong() ? newOrder : match;
                var sellOrder = newOrder.IsLong() ? match : newOrder;

                if (!context.Traders.TryGetValue(buyOrder.TraderId, out var buyer))
                    throw new DomainException($"Покупатель {buyOrder.TraderId} не найден");

                if (!context.Traders.TryGetValue(sellOrder.TraderId, out var seller))
                    throw new DomainException($"Продавец {sellOrder.TraderId} не найден");

                var trade = await Trade.Create(
                    buyer.Id,
                    seller.Id,
                    newOrder.TokenSymbol,
                    tradePrice,
                    tradeQuantity,
                    context.EventPublisher,
                    timeProvider);
                context.AllTrades.Add(trade);

                await buyer.FillOrder(buyOrder.Id, tradeQuantity, tradePrice);
                await seller.FillOrder(sellOrder.Id, tradeQuantity, tradePrice);

                remainingQuantity -= tradeQuantity;
            }
        }

        private async Task UpdateTokenPrice(TradingContext context)
        {
            if (context.AllTrades.Count == 0)
                return;

            var lastTrade = context.AllTrades[^1];
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

            await context.Token.UpdatePrice(lastTrade.Price, now);
        }

        private static List<Order> FindMatchingOrders(Order order, OrderBook orderBook)
        {
            return order.IsLong()
                ? orderBook.Asks.Where(ask => ask.Price <= order.Price).OrderBy(o => o.Price).ToList()
                : orderBook.Bids.Where(bid => bid.Price >= order.Price).OrderByDescending(o => o.Price).ToList();
        }
    }

    internal class TradingContext
    {
        // Исходные данные
        public List<Order> NewOrders { get; set; } = new();
        public List<Order> ExistingOrders { get; set; } = new();
        public Dictionary<long, Trader> Traders { get; set; } = new();
        public Token Token { get; set; } = null!;
        public OrderBook OrderBook { get; set; } = null!;
        public List<Trade> AllTrades { get; set; } = new();

        // Публикатор доменных событий: агрегаты публикуют события сразу, в момент факта
        public IEventPublisher EventPublisher { get; set; } = null!;
    }
}
