using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.TradingContext;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.DomainTests;

public class TradingEngineTest
{
    private readonly TradingEngine _engine = new();
    private readonly RecordingEventPublisher _publisher = new();

    private static Token CreateToken(string symbol = "ZZZ", decimal price = 100m)
        => Token.Create(symbol, "Test Token", TokenRarity.FourStar, price, 1000, "img", "icon");

    private Trader CreateTrader(long id, decimal balance = 1000m)
    {
        var trader = Trader.Create(id, $"User{id}", balance);
        trader.SetEventPublisher(_publisher);
        return trader;
    }

    private Trader CreateTraderWithPortfolio(long id, string symbol, int quantity, decimal avgBuyPrice, decimal balance = 1000m)
    {
        var trader = CreateTrader(id, balance);
        trader.AttachPortfolio(PortfolioItem.Create(id, symbol, quantity, avgBuyPrice));
        return trader;
    }

    private TradingContext CreateContext(
        List<Order>? newOrders = null,
        List<Order>? existingOrders = null,
        Dictionary<long, Trader>? traders = null,
        Token? token = null)
    {
        var context = new TradingContext
        {
            NewOrders = newOrders ?? new List<Order>(),
            ExistingOrders = existingOrders ?? new List<Order>(),
            Traders = traders ?? new Dictionary<long, Trader>(),
            Token = token ?? CreateToken(),
            EventPublisher = _publisher
        };

        context.Token.SetEventPublisher(_publisher);
        foreach (var order in context.ExistingOrders)
            order.SetEventPublisher(_publisher);

        return context;
    }

    [Fact]
    public async Task ProcessOrder_NullOrder_ReturnsFailed()
    {
        var context = CreateContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() => _engine.ProcessOrder(context));

        Assert.Contains("не может быть null", ex.Message);
    }

    [Fact]
    public async Task ProcessOrder_BuyNoMatching_KeepsReservedBalanceAndReturnsOrder()
    {
        var buyer = CreateTrader(1, 10000m);
        var order = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 10);
        var context = CreateContext(
            newOrders: new List<Order> { order },
            traders: new Dictionary<long, Trader> { { 1, buyer } });

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
        Assert.DoesNotContain(_publisher.Events, e => e is TradeExecutedEvent);
        Assert.DoesNotContain(_publisher.Events, e => e is OrderFilledEvent);
        Assert.DoesNotContain(_publisher.Events, e => e is TokenPriceUpdatedEvent);
        Assert.Contains(_publisher.Events, e => e is OrderPlacedEvent placed && placed.Order == order);
        Assert.True(order.IsActive());
        Assert.Equal(10000m - 1000m, buyer.Balance);
    }

    [Fact]
    public async Task ProcessOrder_SellNoMatching_KeepsReservedTokensAndReturnsOrder()
    {
        var seller = CreateTraderWithPortfolio(1, "ZZZ", 10, 50m);
        var order = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 200m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { order },
            traders: new Dictionary<long, Trader> { { 1, seller } });

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
        Assert.DoesNotContain(_publisher.Events, e => e is TradeExecutedEvent);
        Assert.DoesNotContain(_publisher.Events, e => e is OrderFilledEvent);
        Assert.Contains(_publisher.Events, e => e is OrderPlacedEvent placed && placed.Order == order);
        var portfolio = seller.Portfolio.Single();
        Assert.Equal(5, portfolio.ReserveQuantity);
        Assert.Equal(5, portfolio.Quantity);
    }

    [Fact]
    public async Task PlaceOrder_SellWithoutPortfolio_Throws()
    {
        var seller = CreateTrader(1);

        await Assert.ThrowsAsync<DomainException>(() => seller.PlaceOrder(OrderType.Sell, "ZZZ", 100m, 5));
    }

    [Fact]
    public async Task ProcessOrder_BuyMatchingSell_ExecutesTrade()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var token = CreateToken();
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } },
            token: token);

        await _engine.ProcessOrder(context);

        Assert.Single(context.AllTrades);
        Assert.Equal(5, context.AllTrades[0].Quantity);
        Assert.Equal(80m, context.AllTrades[0].Price);
        Assert.Equal(1, context.AllTrades[0].BuyerId);
        Assert.Equal(2, context.AllTrades[0].SellerId);
        Assert.Single(_publisher.Events.OfType<TradeExecutedEvent>());
        Assert.Contains(_publisher.Events, e => e is OrderFilledEvent filled && filled.Order == existingSell);
        Assert.Contains(_publisher.Events, e => e is TokenPriceUpdatedEvent updated && updated.Token == token);
        Assert.True(buyOrder.IsFilled());
        Assert.True(existingSell.IsFilled());
        Assert.Equal(1000m + 80m * 5, seller.Balance);
        Assert.Equal(10000m - 500m + 5 * (100m - 80m), buyer.Balance);
        Assert.Equal(80m, token.CurrentPrice);
    }

    [Fact]
    public async Task ProcessOrder_SellMatchingBuy_ExecutesTrade()
    {
        var buyer = CreateTrader(1);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 10, 50m, balance: 10000m);
        var existingBuy = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 150m, 3);
        var sellOrder = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 120m, 3);
        var context = CreateContext(
            newOrders: new List<Order> { sellOrder },
            existingOrders: new List<Order> { existingBuy },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Single(context.AllTrades);
        Assert.Equal(1, context.AllTrades[0].BuyerId);
        Assert.Equal(2, context.AllTrades[0].SellerId);
        Assert.Equal(150m, context.AllTrades[0].Price);
        Assert.True(sellOrder.IsFilled());
        Assert.True(existingBuy.IsFilled());
        Assert.Equal(150m, context.Token.CurrentPrice);
    }

    [Fact]
    public async Task ProcessOrder_BuyWithOverpayment_RefundsDifference()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 60m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        var overpayment = (100m - 60m) * 5;
        Assert.Equal(10000m - 500m + overpayment, buyer.Balance);
        Assert.Equal(1000m + 60m * 5, seller.Balance);
    }

    [Fact]
    public async Task ProcessOrder_BuyMatchingBuyerHasNoPortfolio_CreatesNewPortfolio()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Single(context.AllTrades);
        Assert.Single(buyer.Portfolio);
        Assert.Equal(5, buyer.Portfolio[0].Quantity);
    }

    [Fact]
    public async Task ProcessOrder_MultipleMatches_FillsPartially()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller1 = CreateTraderWithPortfolio(2, "ZZZ", 3, 50m);
        var seller2 = CreateTraderWithPortfolio(3, "ZZZ", 3, 50m);
        var sell1 = await seller1.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 3);
        var sell2 = await seller2.PlaceOrder(OrderType.Sell, "ZZZ", 90m, 3);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { sell1, sell2 },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller1 }, { 3, seller2 } });

        await _engine.ProcessOrder(context);

        Assert.Equal(2, context.AllTrades.Count);
        Assert.True(buyOrder.IsFilled());
        Assert.True(sell1.IsFilled());
        Assert.Equal(2, sell2.FilledQuantity);
        Assert.Equal(1, sell2.GetRemainingQuantity());
        Assert.False(sell2.IsFilled());
    }

    [Fact]
    public async Task ProcessOrder_BuyExactPriceMatch_NoOverpayment()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 100m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Equal(10000m - 500m, buyer.Balance);
        Assert.Equal(1000m + 500m, seller.Balance);
    }

    [Fact]
    public async Task ProcessOrder_NoTrades_TokenPriceUnchanged()
    {
        var buyer = CreateTrader(1, 10000m);
        var order = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var token = CreateToken(price: 200m);
        var context = CreateContext(
            newOrders: new List<Order> { order },
            traders: new Dictionary<long, Trader> { { 1, buyer } },
            token: token);

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
        Assert.DoesNotContain(_publisher.Events, e => e is TokenPriceUpdatedEvent);
        Assert.Equal(200m, token.CurrentPrice);
    }

    [Fact]
    public async Task ProcessOrder_BuySellOrderFilters_SelfOrdersExcluded()
    {
        var buyer = CreateTraderWithPortfolio(1, "ZZZ", 10, 50m, balance: 10000m);
        var ownSell = await buyer.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { ownSell },
            traders: new Dictionary<long, Trader> { { 1, buyer } });

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
        Assert.True(buyOrder.IsActive());
        Assert.True(ownSell.IsActive());
    }

    [Fact]
    public async Task ProcessOrder_SellBuyOrderFilters_SelfOrdersExcluded()
    {
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 10, 50m, balance: 10000m);
        var ownBuy = await seller.PlaceOrder(OrderType.Buy, "ZZZ", 150m, 5);
        var sellOrder = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { sellOrder },
            existingOrders: new List<Order> { ownBuy },
            traders: new Dictionary<long, Trader> { { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
        Assert.True(sellOrder.IsActive());
    }

    [Fact]
    public async Task ProcessOrder_BuyPartialFillFirstOrderFullyFilled()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller1 = CreateTraderWithPortfolio(2, "ZZZ", 2, 50m);
        var sell1 = await seller1.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 2);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { sell1 },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller1 } });

        await _engine.ProcessOrder(context);

        Assert.Single(context.AllTrades);
        Assert.Equal(2, context.AllTrades[0].Quantity);
        Assert.False(buyOrder.IsFilled());
        Assert.Equal(3, buyOrder.GetRemainingQuantity());
        Assert.True(sell1.IsFilled());
    }

    [Fact]
    public async Task ProcessOrder_SellBuyerPortfolioExists_BuyTokensIncreasesQuantity()
    {
        var buyer = CreateTraderWithPortfolio(1, "ZZZ", 5, 80m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 10, 50m, balance: 10000m);
        var existingBuy = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 150m, 3);
        var sellOrder = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 120m, 3);
        var context = CreateContext(
            newOrders: new List<Order> { sellOrder },
            existingOrders: new List<Order> { existingBuy },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Equal(8, buyer.Portfolio.Single(p => p.TokenSymbol == "ZZZ").Quantity);
    }

    [Fact]
    public async Task ProcessOrder_BuySellNotMatching_DoesNotFill()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 200m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
        Assert.True(buyOrder.IsActive());
        Assert.True(existingSell.IsActive());
    }

    [Fact]
    public async Task ProcessOrder_SellBuyNotMatching_DoesNotFill()
    {
        var buyer = CreateTrader(1);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 10, 50m, balance: 10000m);
        var existingBuy = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 50m, 5);
        var sellOrder = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { sellOrder },
            existingOrders: new List<Order> { existingBuy },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrder(context);

        Assert.Empty(context.AllTrades);
    }

    [Fact]
    public async Task ProcessOrder_BuyTraderMissing_ReturnsFailed()
    {
        var order = Order.Create(OrderType.Buy, "ZZZ", 100m, 5);
        order.TraderId = 1;
        var context = CreateContext(newOrders: new List<Order> { order });

        var ex = await Assert.ThrowsAsync<DomainException>(() => _engine.ProcessOrder(context));

        Assert.Contains("Трейдер не найден", ex.Message);
    }

    [Fact]
    public async Task ProcessOrder_TradeBuyerNotFound_ReturnsFailed()
    {
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 10, 50m, balance: 10000m);
        var sellOrder = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 120m, 3);
        var existingBuy = Order.Create(OrderType.Buy, "ZZZ", 150m, 3);
        existingBuy.TraderId = 1;
        var context = CreateContext(
            newOrders: new List<Order> { sellOrder },
            existingOrders: new List<Order> { existingBuy },
            traders: new Dictionary<long, Trader> { { 2, seller } });

        var ex = await Assert.ThrowsAsync<DomainException>(() => _engine.ProcessOrder(context));

        Assert.Contains("Покупатель", ex.Message);
    }

    [Fact]
    public async Task ProcessOrder_TradeSellerNotFound_ReturnsFailed()
    {
        var buyer = CreateTrader(1, 10000m);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var existingSell = Order.Create(OrderType.Sell, "ZZZ", 80m, 5);
        existingSell.TraderId = 2;
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer } });

        var ex = await Assert.ThrowsAsync<DomainException>(() => _engine.ProcessOrder(context));

        Assert.Contains("Продавец", ex.Message);
    }

    [Fact]
    public async Task ProcessOrder_TradeSellerHasNoPortfolio_ReturnsFailed()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var existingSell = Order.Create(OrderType.Sell, "ZZZ", 80m, 5);
        existingSell.TraderId = 2;
        seller.AttachOrder(existingSell);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        var ex = await Assert.ThrowsAsync<DomainException>(() => _engine.ProcessOrder(context));

        Assert.Contains("No portfolio item", ex.Message);
    }

    [Fact]
    public async Task ProcessOrder_ExecutedTrade_UsesEngineTimeProvider()
    {
        var timeProvider = new TestTimeProvider();
        var engine = new TradingEngine(timeProvider);

        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await engine.ProcessOrder(context);

        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, context.AllTrades[0].ExecutedAt);
    }

    [Fact]
    public async Task ProcessOrders_MatchingOrder_ExecutesTrade()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTraderWithPortfolio(2, "ZZZ", 5, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 5);
        var buyOrder = await buyer.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var context = CreateContext(
            newOrders: new List<Order> { buyOrder },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } });

        await _engine.ProcessOrders(context);

        Assert.Single(context.AllTrades);
        Assert.True(buyOrder.IsFilled());
        Assert.True(existingSell.IsFilled());
    }

    [Fact]
    public async Task ProcessOrders_MultipleNewOrders_MatchesEachAgainstBook()
    {
        var buyer1 = CreateTrader(1, 10000m);
        var buyer2 = CreateTrader(2, 5000m);
        var seller = CreateTraderWithPortfolio(3, "ZZZ", 8, 50m);
        var existingSell = await seller.PlaceOrder(OrderType.Sell, "ZZZ", 80m, 5);
        var buy1 = await buyer1.PlaceOrder(OrderType.Buy, "ZZZ", 100m, 5);
        var buy2 = await buyer2.PlaceOrder(OrderType.Buy, "ZZZ", 90m, 3);
        var context = CreateContext(
            newOrders: new List<Order> { buy1, buy2 },
            existingOrders: new List<Order> { existingSell },
            traders: new Dictionary<long, Trader> { { 1, buyer1 }, { 2, buyer2 }, { 3, seller } });

        await _engine.ProcessOrders(context);

        Assert.Single(context.AllTrades);
        Assert.True(buy1.IsFilled());
        Assert.True(existingSell.IsFilled());
        Assert.True(buy2.IsActive());
        Assert.Equal(2, _publisher.Events.OfType<OrderPlacedEvent>().Count(e => context.NewOrders.Contains(e.Order)));
    }

    [Fact]
    public async Task ProcessOrders_EmptyOrders_ReturnsFailed()
    {
        var context = CreateContext();

        var ex = await Assert.ThrowsAsync<DomainException>(() => _engine.ProcessOrders(context));

        Assert.Contains("пустым", ex.Message);
    }
}
