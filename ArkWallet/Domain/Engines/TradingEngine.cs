using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Domain.Engines
{
    internal class TradingEngine
    {
        private readonly Dictionary<string, OrderBook> _orderBooks = new();

        public TradingResult ProcessOrder(
            TradeOrder newOrder,
            List<TradeOrder> existingOrders,
            Dictionary<long, Trader> traders,
            Dictionary<long, PortfolioItem> portfolios,
            CharacterToken token)
        {
            // ВАЛИДАЦИЯ (только логическая)
            if (newOrder == null)
                return TradingResult.Failed("Ордер не может быть null");

            if (newOrder.Quantity <= 0 || newOrder.Price <= 0)
                return TradingResult.Failed("Количество и цена должны быть > 0");

            // Загружаем стакан из существующих ордеров
            var orderBook = GetOrCreateOrderBook(newOrder.CharacterTokenId);
            orderBook.LoadOrders(existingOrders, newOrder.TraderTelegramId);

            // МАТЧИНГ
            var matches = FindMatchingOrders(newOrder, orderBook);
            var trades = new List<Trade>();
            var remainingQuantity = newOrder.Quantity;

            foreach (var match in matches)
            {
                if (remainingQuantity <= 0) break;

                var tradeQuantity = Math.Min(remainingQuantity, match.GetRemainingQuantity());
                var tradePrice = match.Price;

                // СОЗДАЕМ СДЕЛКУ
                var trade = CreateTrade(newOrder, match, tradeQuantity, tradePrice);
                trades.Add(trade);

                // 🔥 РАССЧИТЫВАЕМ ИЗМЕНЕНИЯ (НЕ сохраняем в БД!)
                UpdateTradersAndPortfolios(traders, portfolios, trade, tradeQuantity, tradePrice);

                // ОБНОВЛЯЕМ ОРДЕРА
                UpdateOrderFill(newOrder, tradeQuantity);
                UpdateOrderFill(match, tradeQuantity);

                remainingQuantity -= tradeQuantity;
            }

            // 🔥 ВОЗВРАЩАЕМ РЕЗУЛЬТАТ ДЛЯ СОХРАНЕНИЯ
            return new TradingResult
            {
                Trades = trades,
                UpdatedOrders = GetUpdatedOrders(existingOrders, matches),
                UpdatedTraders = traders.Values.Where(t => t.IsDirty).ToList(),
                UpdatedPortfolios = portfolios.Values.Where(p => p.IsDirty).ToList(),
                OrderToAdd = remainingQuantity > 0 ? newOrder.WithQuantity(remainingQuantity) : null,
                UpdatedToken = UpdateTokenPrice(token, trades),
                IsSuccess = true
            };
        }

        private List<TradeOrder> GetUpdatedOrders(List<TradeOrder> existingOrders, List<TradeOrder> matchedOrders)
        {
            var updatedOrders = new List<TradeOrder>();

            foreach (var match in matchedOrders)
            {
                if (match.FilledQuantity > 0)
                {
                    updatedOrders.Add(match);
                }
            }

            return updatedOrders.Distinct().ToList();
        }

        private void UpdateTradersAndPortfolios(
            Dictionary<long, Trader> traders,
            Dictionary<long, PortfolioItem> portfolios,
            Trade trade, int quantity, decimal price)
        {
            var totalAmount = quantity * price;
            var buyer = traders[trade.BuyerId];
            var seller = traders[trade.SellerId];
            var buyerPortfolio = portfolios[trade.BuyerId];
            var sellerPortfolio = portfolios[trade.SellerId];

            // Обновляем балансы
            buyer.Balance -= totalAmount;
            seller.Balance += totalAmount;

            // Обновляем портфели
            buyerPortfolio.AddTokens(quantity, price);
            sellerPortfolio.RemoveTokens(quantity);

            buyer.MarkDirty();
            seller.MarkDirty();
            buyerPortfolio.MarkDirty();
            sellerPortfolio.MarkDirty();
        }

        private List<TradeOrder> FindMatchingOrders(TradeOrder order, OrderBook orderBook)
        {
            return order.Type == OrderType.Buy
                ? orderBook.Asks.Where(ask => ask.Price <= order.Price).ToList()
                : orderBook.Bids.Where(bid => bid.Price >= order.Price).ToList();
        }

        private CharacterToken UpdateTokenPrice(CharacterToken token, List<Trade> trades)
        {
            if (trades.Any())
            {
                // Обновляем цену токена на основе последней сделки
                var lastTrade = trades.Last();
                token.UpdatePrice(lastTrade.Price);
                return token;
            }
            return token;
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
