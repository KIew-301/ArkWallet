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

            if (newOrder.IsLong())
            {
                var buyer = traders[newOrder.TraderTelegramId];
                buyer.AddToBalance(-newOrder.GetReservedBalance());
                buyer.MarkDirty();
            }
            else
            {
                if (!portfolios.TryGetValue(newOrder.TraderTelegramId, out var sellerPortfolio))
                    return TradingResult.Failed("В портфеле отсуствует данный токен");

                sellerPortfolio.RemoveTokens(newOrder.Quantity);
                sellerPortfolio.MarkDirty();
            }

            // Загружаем стакан из существующих ордеров
            var orderBook = CreateOrderBook(newOrder.CharacterTokenId);
            orderBook.LoadOrders(existingOrders, newOrder.TraderTelegramId);

            // МАТЧИНГ
            var matches = FindMatchingOrders(newOrder, orderBook);
            var trades = new List<Trade>();
            var traderIdWithNewPortfolio = new List<long>();
            var remainingQuantity = newOrder.Quantity;

            foreach (var match in matches)
            {
                if (remainingQuantity <= 0) break;

                var tradeQuantity = Math.Min(remainingQuantity, match.GetRemainingQuantity());
                var tradePrice = match.Price;

                // СОЗДАЕМ СДЕЛКУ
                var trade = CreateTrade(newOrder, match, tradeQuantity, tradePrice);
                trades.Add(trade);

                if (!portfolios.ContainsKey(trade.BuyerId))
                    traderIdWithNewPortfolio.Add(trade.BuyerId);

                // 🔥 РАССЧИТЫВАЕМ ИЗМЕНЕНИЯ (НЕ сохраняем в БД!)
                UpdateTradersAndPortfolios(traders, portfolios, trade, tradeQuantity, tradePrice, newOrder.Price);

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
                UpdatedPortfolios = portfolios.Values.Where(p => p.IsDirty && !traderIdWithNewPortfolio.Contains(p.TraderTelegramId)).ToList(),
                OrderToAdd = newOrder,
                PortfoliosToAdd = portfolios.Values.Where(p => p.IsDirty && traderIdWithNewPortfolio.Contains(p.TraderTelegramId)).ToList(),
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
            Trade trade, int quantity, decimal price,
            decimal buyerOrderPrice)
        {
            var totalAmount = quantity * price;

            // Обновляем балансы
            var buyer = traders[trade.BuyerId];
            var seller = traders[trade.SellerId];

            seller.AddToBalance(totalAmount);

            var overpayment = (buyerOrderPrice - price) * quantity;
            if (overpayment > 0)
            {
                buyer.AddToBalance(overpayment);
            }

            // Обновляем портфели
            var buyerPortfolio = portfolios.ContainsKey(trade.BuyerId) ? portfolios[trade.BuyerId] : null;

            if (buyerPortfolio != null)
            {
                buyerPortfolio.AddTokens(quantity, price);
            }
            else
            {
                buyerPortfolio = PortfolioItem.Create(trade.BuyerId, trade.CharacterTokenId, quantity, price);
                portfolios[trade.BuyerId] = buyerPortfolio;
            }

            buyer.MarkDirty();
            seller.MarkDirty();
            buyerPortfolio.MarkDirty();
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
        private OrderBook CreateOrderBook(string characterTokenId)
        {
            return _orderBooks[characterTokenId] = new OrderBook();
        }
    }
}
