using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Domain.Engines
{
    internal class TradingEngine(TimeProvider? timeProvider = null)
    {
        private readonly Dictionary<string, OrderBook> _orderBooks = new();

        public Result ProcessOrder(TradingContext context)
        {
            try
            {
                if (context.NewOrders == null || context.NewOrders.Count == 0)
                    return Result.Fail("Ордер не может быть null");

                var newOrder = context.NewOrders[0];

                if (newOrder.Quantity <= 0 || newOrder.Price <= 0)
                    return Result.Fail("Количество и цена должны быть > 0");

                var orderBook = CreateOrGetOrderBook(context.Token.Symbol);
                orderBook.LoadOrders(context.ExistingOrders, newOrder.TraderTelegramId);
                context.OrderBook = orderBook;

                ProcessSingleOrder(newOrder, context);

                if (context.AllTrades.Count > 0)
                {
                    var lastTrade = context.AllTrades[^1];
                    context.Token.UpdatePrice(lastTrade.Price);
                    context.ModifiedTokens.Add(context.Token);
                }

                return Result.Ok();
            }
            catch (DomainException ex)
            {
                return Result.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return Result.Fail(ex.Message);
            }
        }

        public Result ProcessOrders(TradingContext context)
        {
            try
            {
                if (context.NewOrders == null || context.NewOrders.Count == 0)
                    return Result.Fail("Список ордеров не может быть пустым");

                foreach (var newOrder in context.NewOrders)
                {
                    var orderBook = CreateOrGetOrderBook(context.Token.Symbol);
                    orderBook.LoadOrders(context.ExistingOrders, newOrder.TraderTelegramId);
                    context.OrderBook = orderBook;

                    ProcessSingleOrder(newOrder, context);
                }

                if (context.AllTrades.Count > 0)
                {
                    var lastTrade = context.AllTrades[^1];
                    context.Token.UpdatePrice(lastTrade.Price);
                    context.ModifiedTokens.Add(context.Token);
                }

                return Result.Ok();
            }
            catch (DomainException ex)
            {
                return Result.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return Result.Fail(ex.Message);
            }
        }

        private void ProcessSingleOrder(TradeOrder newOrder, TradingContext context)
        {
            if (newOrder == null)
                throw new DomainException("Ордер не может быть null");

            if (newOrder.Quantity <= 0 || newOrder.Price <= 0)
                throw new DomainException("Количество и цена должны быть > 0");

            ReserveOrderFunds(newOrder, context);

            var matches = FindMatchingOrders(newOrder, context.OrderBook);
            var remainingQuantity = newOrder.Quantity;

            if (matches.Count == 0)
            {
                context.NewOrdersToAdd.Add(newOrder);
                return;
            }

            foreach (var match in matches)
            {
                if (remainingQuantity <= 0) break;

                var tradeQuantity = Math.Min(remainingQuantity, match.GetRemainingQuantity());
                var tradePrice = match.Price;

                var trade = CreateTrade(newOrder, match, tradeQuantity, tradePrice);
                context.NewTradesToAdd.Add(trade);
                context.AllTrades.Add(trade);

                UpdateTradersAndPortfolios(context, trade, tradeQuantity, tradePrice, newOrder.Price);

                newOrder.UpdateOrderFill(tradeQuantity, trade.Price);
                match.UpdateOrderFill(tradeQuantity, trade.Price);

                if (match.IsFilled() || match.FilledQuantity > 0)
                {
                    context.ModifiedOrders.Add(match);
                }

                remainingQuantity -= tradeQuantity;
            }

            context.NewOrdersToAdd.Add(newOrder);
        }

        private static void ReserveOrderFunds(TradeOrder newOrder, TradingContext context)
        {
            if (newOrder.IsLong())
            {
                if (!context.Traders.TryGetValue(newOrder.TraderTelegramId, out var buyer))
                    throw new DomainException("Трейдер не найден");

                buyer.AddToBalance(-newOrder.GetReservedBalance());
                context.ModifiedTraders.Add(buyer);
            }
            else
            {
                if (!context.Portfolios.TryGetValue(newOrder.TraderTelegramId, out var sellerPortfolio))
                    throw new DomainException("В портфеле отсутствует данный токен");

                sellerPortfolio.ReserveTokens(newOrder.Quantity, newOrder.Price);
                context.ModifiedPortfolios.Add(sellerPortfolio);
            }
        }

        private void UpdateTradersAndPortfolios(
            TradingContext context,
            Trade trade,
            int quantity,
            decimal price,
            decimal buyerOrderPrice)
        {
            var totalAmount = quantity * price;

            if (!context.Traders.TryGetValue(trade.BuyerId, out var buyer))
                throw new DomainException($"Покупатель {trade.BuyerId} не найден");

            if (!context.Traders.TryGetValue(trade.SellerId, out var seller))
                throw new DomainException($"Продавец {trade.SellerId} не найден");

            seller.AddToBalance(totalAmount);

            var overpayment = (buyerOrderPrice - price) * quantity;
            if (overpayment > 0)
            {
                buyer.AddToBalance(overpayment);
            }

            context.ModifiedTraders.Add(buyer);
            context.ModifiedTraders.Add(seller);

            if (context.Portfolios.TryGetValue(trade.BuyerId, out var buyerPortfolio))
            {
                buyerPortfolio.BuyTokens(quantity, price);
                context.ModifiedPortfolios.Add(buyerPortfolio);
            }
            else
            {
                buyerPortfolio = PortfolioItem.Create(trade.BuyerId, trade.CharacterTokenId, quantity, price);
                context.NewPortfoliosToAdd.Add(buyerPortfolio);
                context.Portfolios[trade.BuyerId] = buyerPortfolio;
            }

            if (context.Portfolios.TryGetValue(trade.SellerId, out var sellerPortfolio))
            {
                sellerPortfolio.SellTokens(quantity, price);
                context.ModifiedPortfolios.Add(sellerPortfolio);
            }
            else
            {
                throw new DomainException("Продавец не имеет портфеля");
            }
        }

        private List<TradeOrder> FindMatchingOrders(TradeOrder order, OrderBook orderBook)
        {
            return order.Type == OrderType.Buy
                ? orderBook.Asks.Where(ask => ask.Price <= order.Price).OrderBy(o => o.Price).ToList()
                : orderBook.Bids.Where(bid => bid.Price >= order.Price).OrderByDescending(o => o.Price).ToList();
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
                ExecutedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime
            };
        }

        private OrderBook CreateOrGetOrderBook(string characterTokenId)
        {
            if (!_orderBooks.ContainsKey(characterTokenId))
                _orderBooks[characterTokenId] = new OrderBook();

            return _orderBooks[characterTokenId];
        }
    }

    internal class TradingContext
    {
        // Исходные данные
        public List<TradeOrder> NewOrders { get; set; } = new();
        public List<TradeOrder> ExistingOrders { get; set; } = new();
        public Dictionary<long, Trader> Traders { get; set; } = new();
        public Dictionary<long, PortfolioItem> Portfolios { get; set; } = new();
        public CharacterToken Token { get; set; } = null!;
        public OrderBook OrderBook { get; set; } = null!;
        public List<Trade> AllTrades { get; set; } = new();

        // Явные списки для сохранения
        public List<TradeOrder> NewOrdersToAdd { get; set; } = new();
        public List<TradeOrder> ModifiedOrders { get; set; } = new();
        public List<Trade> NewTradesToAdd { get; set; } = new();
        public List<Trader> ModifiedTraders { get; set; } = new();
        public List<PortfolioItem> NewPortfoliosToAdd { get; set; } = new();
        public List<PortfolioItem> ModifiedPortfolios { get; set; } = new();
        public List<CharacterToken> ModifiedTokens { get; set; } = new();
    }
}