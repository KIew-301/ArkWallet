using ArkWallet.Entities;
using ArkWallet.Repositories;
using ArkWallet.ValueObjects;

namespace ArkWallet.Domain
{
    internal class TradingEngine
    {
        private readonly Dictionary<string, OrderBook> _orderBooks = new();

        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;

        public TradingEngine(TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo)
        {
            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
        }

        public async Task<OrderResult> PlaceOrder(TradeOrder order)
        {
            if (order == null)
                return OrderResult.Failed(null, "Ордер не может быть null");

            if (order.Quantity <= 0)
                return OrderResult.Failed(order, "Количество должно быть больше 0");

            if (order.Price <= 0)
                return OrderResult.Failed(order, "Цена должна быть больше 0");

            if (string.IsNullOrEmpty(order.CharacterTokenId))
                return OrderResult.Failed(order, "Не указан токен");

            // Токен существует
            var token = await _tokenRepo.GetByIdAsync(order.CharacterTokenId);
            if (token == null)
                return OrderResult.Failed(order, $"Токен {order.CharacterTokenId} не найден");

            // Трейдер существует
            var trader = await _traderRepo.GetByIdAsync(order.TraderTelegramId);
            if (trader == null)
                return OrderResult.Failed(order, $"Трейдер {order.TraderTelegramId} не найден");

            // Токенов достаточно для продажи
            if (order.Type == OrderType.Sell)
            {
                var portfolio = await _portfolioRepo.GetBySymbolAndOwnerAsync(
                    order.TraderTelegramId, order.CharacterTokenId);

                if (portfolio == null || portfolio.Quantity < order.Quantity)
                    return OrderResult.Failed(order, "Недостаточно токенов для продажи");
            }

            // Токенов достаточно для покупки
            if (order.Type == OrderType.Buy)
            {
                var totalCost = order.Quantity * order.Price;
                if (trader.Balance < totalCost)
                    return OrderResult.Failed(order, "Недостаточно средств");
            }

            var orderBook = GetOrCreateOrderBook(order.CharacterTokenId);

            // Ищем подходящие ордера для матчинга
            var matches = FindMatchingOrders(order, orderBook);

            if (matches.Any())
            {
                return await ExecuteTrades(order, matches, orderBook);
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

        private async Task<OrderResult> ExecuteTrades(TradeOrder order, List<TradeOrder> matches, OrderBook orderBook)
        {
            var trades = new List<Trade>();
            var remainingQuantity = order.Quantity;

            foreach (var match in matches.OrderBy(m => m.Type == OrderType.Sell ? m.Price : -m.Price))
            {
                if (remainingQuantity <= 0) break;

                var tradeQuantity = Math.Min(remainingQuantity, match.GetRemainingQuantity());
                var tradePrice = match.Price;
                var totalAmount = tradeQuantity * tradePrice;

                // 1. Создаем сделку
                var trade = CreateTrade(order, match, tradeQuantity, tradePrice);
                trades.Add(trade);

                // 🔥 2. ОБНОВЛЯЕМ ДАННЫЕ В БД
                await UpdateTokenPrice(order.CharacterTokenId, tradePrice);
                await UpdatePortfolios(trade.BuyerId, trade.SellerId, order.CharacterTokenId, tradeQuantity, tradePrice);
                await UpdateBalances(trade.BuyerId, trade.SellerId, totalAmount);

                // 3. Обновляем ордера в памяти
                UpdateOrderFill(order, tradeQuantity);
                UpdateOrderFill(match, tradeQuantity);

                if (match.IsFilled())
                {
                    RemoveFromOrderBook(match, orderBook);
                    match.MarkAsFilled();
                }

                remainingQuantity -= tradeQuantity;
            }

            if (remainingQuantity > 0 && !order.IsFilled())
            {
                order.Quantity = remainingQuantity;
                AddToOrderBook(order, orderBook);
            }

            return OrderResult.Filled(order, trades);
        }

        private async Task UpdateTokenPrice(string characterTokenId, decimal tradePrice)
        {
            var token = await _tokenRepo.GetByIdAsync(characterTokenId);
            if (token != null)
            {
                token.UpdatePrice(tradePrice);
                await _tokenRepo.UpdateAsync(token);
            }
        }

        private async Task UpdatePortfolios(long buyerId, long sellerId, string tokenSymbol, int quantity, decimal price)
        {
            // Покупатель получает токены
            await _portfolioRepo.AddOrUpdateAsync(buyerId, tokenSymbol, quantity, price);

            // Продавец теряет токены
            await _portfolioRepo.RemoveOrUpdateAsync(sellerId, tokenSymbol, quantity);
        }

        private async Task UpdateBalances(long buyerId, long sellerId, decimal totalAmount)
        {
            // Покупатель платит
            await _traderRepo.DeductBalanceAsync(buyerId, totalAmount);

            // Продавец получает
            await _traderRepo.AddBalanceAsync(sellerId, totalAmount);
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
