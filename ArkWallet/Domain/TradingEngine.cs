using ArkWallet.Entities;
using ArkWallet.ValueObjects;

namespace ArkWallet.Domain
{
    internal class TradingEngine
    {
        private readonly Dictionary<string, OrderBook> _orderBooks = new();

        public OrderResult PlaceOrder(TradeOrder order)
        {
            var orderBook = GetOrCreateOrderBook(order.CharacterTokenId);

            // Ищем подходящие ордера для матчинга
            var matches = FindMatchingOrders(order, orderBook);

            if (matches.Any())
            {
                return ExecuteTrades(order, matches, orderBook);
            }
            else
            {
                // Добавляем в стакан если нет матчей
                AddToOrderBook(order, orderBook);
                return OrderResult.Pending(order);
            }
        }

        private List<TradeOrder> FindMatchingOrders(TradeOrder order, OrderBook orderBook)
        {
            return order.Type == OrderType.Buy
                ? orderBook.Asks.Where(ask => ask.Price <= order.Price).ToList()
                : orderBook.Bids.Where(bid => bid.Price >= order.Price).ToList();
        }

        private OrderResult ExecuteTrades(TradeOrder order, List<TradeOrder> matches, OrderBook orderBook)
        {
            var trades = new List<Trade>();
            var remainingQuantity = order.Quantity;

            foreach (var match in matches.OrderBy(m => m.Type == OrderType.Sell ? m.Price : -m.Price))
            {
                if (remainingQuantity <= 0) break;

                var tradeQuantity = Math.Min(remainingQuantity, match.GetRemainingQuantity());
                var tradePrice = match.Price; // Исполняем по цене встречного ордера

                var trade = CreateTrade(order, match, tradeQuantity, tradePrice);
                trades.Add(trade);

                // Обновляем ордера
                UpdateOrderFill(order, tradeQuantity);
                UpdateOrderFill(match, tradeQuantity);

                // Убираем из стакана если исполнен
                if (match.IsFilled())
                {
                    RemoveFromOrderBook(match, orderBook);
                    match.MarkAsFilled();
                }

                remainingQuantity -= tradeQuantity;
            }

            // Если ордер исполнен не полностью - добавляем остаток в стакан
            if (remainingQuantity > 0 && !order.IsFilled())
            {
                order.Quantity = remainingQuantity;
                AddToOrderBook(order, orderBook);
            }

            return OrderResult.Filled(order, trades);
        }

        private Trade CreateTrade(TradeOrder order, TradeOrder match, int quantity, decimal price)
        {
            return new Trade
            {
                BuyerId = order.Type == OrderType.Buy ? order.TraderTelegramId : match.TraderTelegramId,
                SellerId = order.Type == OrderType.Sell ? order.TraderTelegramId : match.TraderTelegramId,
                CharacterTokenId = order.CharacterTokenId,
                Price = price,
                Quantity = quantity,
                ExecutedAt = DateTime.UtcNow
            };
        }

        private void UpdateOrderFill(TradeOrder order, int filledQuantity)
        {
            order.FilledQuantity += filledQuantity;
            if (order.IsFilled())
            {
                order.MarkAsFilled();
            }
        }

        private OrderBook GetOrCreateOrderBook(string characterTokenId)
        {
            if (!_orderBooks.ContainsKey(characterTokenId))
            {
                _orderBooks[characterTokenId] = new OrderBook();
            }
            return _orderBooks[characterTokenId];
        }

        private void AddToOrderBook(TradeOrder order, OrderBook orderBook)
        {
            if (order.Type == OrderType.Buy)
                orderBook.Bids.Add(order);
            else
                orderBook.Asks.Add(order);
        }

        private void RemoveFromOrderBook(TradeOrder order, OrderBook orderBook)
        {
            if (order.Type == OrderType.Buy)
                orderBook.Bids.Remove(order);
            else
                orderBook.Asks.Remove(order);
        }
    }
}
