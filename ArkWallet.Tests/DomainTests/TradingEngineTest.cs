using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests.DomainTests;

public class TradingEngineTest
{
    private readonly TradingEngine _engine = new();

    private static CharacterToken CreateToken(string symbol = "ZZZ", decimal price = 100m)
    {
        return CharacterToken.Create(symbol, "Test Token", CharacterRarity.FourStar, price, 1000, "img", "icon");
    }

    private static Trader CreateTrader(long id, decimal? balance = null)
    {
        var trader = Trader.Create(id, $"User{id}");
        if (balance.HasValue)
            trader.AddToBalance(balance.Value - Trader.GetDefaultBalance());
        return trader;
    }

    private static TradeOrder CreateBuyOrder(long traderId, string symbol = "ZZZ", int quantity = 5, decimal price = 100m)
        => TradeOrder.Create(OrderType.Buy, symbol, traderId, price, quantity);

    private static TradeOrder CreateSellOrder(long traderId, string symbol = "ZZZ", int quantity = 5, decimal price = 100m)
        => TradeOrder.Create(OrderType.Sell, symbol, traderId, price, quantity);

    private static TradingContext CreateContext(
        TradeOrder newOrder,
        List<TradeOrder> existingOrders = null,
        Dictionary<long, Trader> traders = null,
        Dictionary<long, PortfolioItem> portfolios = null,
        CharacterToken token = null)
    {
        return new TradingContext
        {
            NewOrders = new List<TradeOrder> { newOrder },
            ExistingOrders = existingOrders ?? new List<TradeOrder>(),
            Traders = traders ?? new Dictionary<long, Trader>(),
            Portfolios = portfolios ?? new Dictionary<long, PortfolioItem>(),
            Token = token ?? CreateToken(),
            AllTrades = new List<Trade>()
        };
    }

    [Fact]
    public void ProcessOrder_NullOrder_ReturnsFailed()
    {
        var context = new TradingContext
        {
            NewOrders = null,
            ExistingOrders = new List<TradeOrder>(),
            Traders = new Dictionary<long, Trader>(),
            Portfolios = new Dictionary<long, PortfolioItem>(),
            Token = CreateToken(),
            AllTrades = new List<Trade>()
        };

        var result = _engine.ProcessOrder(context);
        Assert.False(result.IsSuccess);
        Assert.Contains("не может быть null", result.Message);
    }

    [Fact]
    public void ProcessOrder_ZeroQuantity_ReturnsFailed()
    {
        var order = new TradeOrder { Quantity = 0, Price = 100, Type = OrderType.Buy, CharacterTokenId = "ZZZ", TraderTelegramId = 1 };
        var context = CreateContext(order);

        var result = _engine.ProcessOrder(context);
        Assert.False(result.IsSuccess);
        Assert.Contains("Количество", result.Message);
    }

    [Fact]
    public void ProcessOrder_ZeroPrice_ReturnsFailed()
    {
        var order = new TradeOrder { Quantity = 5, Price = 0, Type = OrderType.Buy, CharacterTokenId = "ZZZ", TraderTelegramId = 1 };
        var context = CreateContext(order);

        var result = _engine.ProcessOrder(context);
        Assert.False(result.IsSuccess);
        Assert.Contains("цена", result.Message);
    }

    [Fact]
    public void ProcessOrder_BuyNoMatching_ReservesBalanceAndReturnsOrder()
    {
        var buyer = CreateTrader(1, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer } };
        var order = CreateBuyOrder(1, quantity: 10, price: 100m);
        var context = CreateContext(order, traders: traders);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
        Assert.True(order.IsActive());
        Assert.Equal(10000m - 1000m, buyer.Balance);
    }

    [Fact]
    public void ProcessOrder_SellNoMatching_ReservesTokensAndReturnsOrder()
    {
        var seller = CreateTrader(1);
        var traders = new Dictionary<long, Trader> { { 1, seller } };
        var portfolio = PortfolioItem.Create(1, "ZZZ", 10, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 1, portfolio } };
        var order = CreateSellOrder(1, quantity: 5, price: 200m);
        var context = CreateContext(order, traders: traders, portfolios: portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
        Assert.Equal(5, portfolio.ReserveQuantity);
        Assert.Equal(5, portfolio.Quantity);
        Assert.True(portfolio.IsDirty);
    }

    [Fact]
    public void ProcessOrder_SellWithoutPortfolio_ReturnsFailed()
    {
        var seller = CreateTrader(1);
        var traders = new Dictionary<long, Trader> { { 1, seller } };
        var order = CreateSellOrder(1);
        var context = CreateContext(order, traders: traders);

        var result = _engine.ProcessOrder(context);

        Assert.False(result.IsSuccess);
        Assert.Contains("портфеле", result.Message);
    }

    [Fact]
    public void ProcessOrder_BuyMatchingSell_ExecutesTrade()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingSell = CreateSellOrder(2, quantity: 5, price: 80m);
        var existingOrders = new List<TradeOrder> { existingSell };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 5, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var token = CreateToken();
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios, token);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Single(context.AllTrades);
        Assert.Equal(5, context.AllTrades[0].Quantity);
        Assert.Equal(80m, context.AllTrades[0].Price);
        Assert.Equal(1, context.AllTrades[0].BuyerId);
        Assert.Equal(2, context.AllTrades[0].SellerId);
        Assert.True(buyOrder.IsFilled());
        Assert.True(existingSell.IsFilled());
        Assert.Equal(1000m + 80m * 5, seller.Balance);
        Assert.Equal(10000m - 500m + 5 * (100m - 80m), buyer.Balance);
        Assert.Equal(80m, token.CurrentPrice);
    }

    [Fact]
    public void ProcessOrder_SellMatchingBuy_ExecutesTrade()
    {
        var buyer = CreateTrader(1);
        var seller = CreateTrader(2, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingBuy = CreateBuyOrder(1, quantity: 3, price: 150m);
        var existingOrders = new List<TradeOrder> { existingBuy };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 10, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var sellOrder = CreateSellOrder(2, quantity: 3, price: 120m);
        var token = CreateToken();
        var context = CreateContext(sellOrder, existingOrders, traders, portfolios, token);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Single(context.AllTrades);
        Assert.Equal(1, context.AllTrades[0].BuyerId);
        Assert.Equal(2, context.AllTrades[0].SellerId);
        Assert.Equal(150m, context.AllTrades[0].Price);
        Assert.True(sellOrder.IsFilled());
        Assert.True(existingBuy.IsFilled());
        Assert.Equal(150m, token.CurrentPrice);
    }

    [Fact]
    public void ProcessOrder_BuyWithOverpayment_RefundsDifference()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingSell = CreateSellOrder(2, quantity: 5, price: 60m);
        var existingOrders = new List<TradeOrder> { existingSell };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 5, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        var overpayment = (100m - 60m) * 5;
        Assert.Equal(10000m - 500m + overpayment, buyer.Balance);
        Assert.Equal(1000m + 60m * 5, seller.Balance);
    }

    [Fact]
    public void ProcessOrder_BuyMatchingBuyerHasNoPortfolio_CreatesNewPortfolio()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingSell = CreateSellOrder(2, quantity: 5, price: 80m);
        var existingOrders = new List<TradeOrder> { existingSell };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 5, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Single(context.AllTrades);
        Assert.True(context.Portfolios.ContainsKey(1));
        Assert.Equal(5, context.Portfolios[1].Quantity);
    }

    [Fact]
    public void ProcessOrder_MultipleMatches_FillsPartially()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller1 = CreateTrader(2);
        var seller2 = CreateTrader(3);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller1 }, { 3, seller2 } };

        var sell1 = CreateSellOrder(2, quantity: 3, price: 80m);
        var sell2 = CreateSellOrder(3, quantity: 3, price: 90m);
        var existingOrders = new List<TradeOrder> { sell1, sell2 };

        var seller1Portfolio = PortfolioItem.Create(2, "ZZZ", 3, 50m);
        var seller2Portfolio = PortfolioItem.Create(3, "ZZZ", 3, 50m);
        var portfolios = new Dictionary<long, PortfolioItem>
        {
            { 2, seller1Portfolio }, { 3, seller2Portfolio }
        };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, context.AllTrades.Count);
        Assert.True(buyOrder.IsFilled());
        Assert.True(sell1.IsFilled());
        Assert.Equal(2, sell2.FilledQuantity);
        Assert.Equal(1, sell2.GetRemainingQuantity());
        Assert.False(sell2.IsFilled());
    }

    [Fact]
    public void ProcessOrder_BuyExactPriceMatch_NoOverpayment()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingSell = CreateSellOrder(2, quantity: 5, price: 100m);
        var existingOrders = new List<TradeOrder> { existingSell };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 5, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Equal(10000m - 500m, buyer.Balance);
        Assert.Equal(1000m + 500m, seller.Balance);
    }

    [Fact]
    public void ProcessOrder_NoTrades_TokenPriceUnchanged()
    {
        var buyer = CreateTrader(1, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer } };
        var order = CreateBuyOrder(1);
        var token = CreateToken(price: 200m);
        var context = CreateContext(order, traders: traders, token: token);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
        Assert.Equal(200m, token.CurrentPrice);
    }

    [Fact]
    public void ProcessOrder_BuySellOrderFilters_SelfOrdersExcluded()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var ownBuyOrder = CreateBuyOrder(1, quantity: 5, price: 200m);
        var existingOrders = new List<TradeOrder> { ownBuyOrder };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 5, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
        Assert.True(buyOrder.IsActive());
    }

    [Fact]
    public void ProcessOrder_SellBuyOrderFilters_SelfOrdersExcluded()
    {
        var buyer = CreateTrader(1);
        var seller = CreateTrader(2, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var ownSellOrder = CreateSellOrder(2, quantity: 5, price: 50m);
        var existingOrders = new List<TradeOrder> { ownSellOrder };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 10, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var sellOrder = CreateSellOrder(2, quantity: 5, price: 100m);
        var context = CreateContext(sellOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
    }

    [Fact]
    public void ProcessOrder_BuyPartialFillFirstOrderFullyFilled()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller1 = CreateTrader(2);
        var seller2 = CreateTrader(3);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller1 }, { 3, seller2 } };

        var sell1 = CreateSellOrder(2, quantity: 2, price: 80m);
        var existingOrders = new List<TradeOrder> { sell1 };

        var seller1Portfolio = PortfolioItem.Create(2, "ZZZ", 2, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, seller1Portfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Single(context.AllTrades);
        Assert.Equal(2, context.AllTrades[0].Quantity);
        Assert.False(buyOrder.IsFilled());
        Assert.Equal(3, buyOrder.GetRemainingQuantity());
        Assert.True(sell1.IsFilled());
    }

    [Fact]
    public void ProcessOrder_SellBuyerPortfolioExists_BuyTokensIncreasesQuantity()
    {
        var buyer = CreateTrader(1);
        var seller = CreateTrader(2, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingBuy = CreateBuyOrder(1, quantity: 3, price: 150m);
        var existingOrders = new List<TradeOrder> { existingBuy };

        var buyerPortfolio = PortfolioItem.Create(1, "ZZZ", 5, 80m);
        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 10, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 1, buyerPortfolio }, { 2, sellerPortfolio } };

        var sellOrder = CreateSellOrder(2, quantity: 3, price: 120m);
        var context = CreateContext(sellOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, buyerPortfolio.Quantity);
        Assert.True(buyerPortfolio.IsDirty);
    }

    [Fact]
    public void ProcessOrder_BuySellNotMatching_DoesNotFill()
    {
        var buyer = CreateTrader(1, 10000m);
        var seller = CreateTrader(2);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingSell = CreateSellOrder(2, quantity: 5, price: 200m);
        var existingOrders = new List<TradeOrder> { existingSell };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 5, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var buyOrder = CreateBuyOrder(1, quantity: 5, price: 100m);
        var context = CreateContext(buyOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
        Assert.True(buyOrder.IsActive());
        Assert.True(existingSell.IsActive());
    }

    [Fact]
    public void ProcessOrder_SellBuyNotMatching_DoesNotFill()
    {
        var buyer = CreateTrader(1);
        var seller = CreateTrader(2, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer }, { 2, seller } };

        var existingBuy = CreateBuyOrder(1, quantity: 5, price: 50m);
        var existingOrders = new List<TradeOrder> { existingBuy };

        var sellerPortfolio = PortfolioItem.Create(2, "ZZZ", 10, 50m);
        var portfolios = new Dictionary<long, PortfolioItem> { { 2, sellerPortfolio } };

        var sellOrder = CreateSellOrder(2, quantity: 5, price: 100m);
        var context = CreateContext(sellOrder, existingOrders, traders, portfolios);

        var result = _engine.ProcessOrder(context);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.AllTrades);
    }
}