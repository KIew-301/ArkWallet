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

    [Fact]
    public void ProcessOrder_NullOrder_ReturnsFailed()
    {
        var result = _engine.ProcessOrder(null!, new(), new(), new(), CreateToken());
        Assert.False(result.IsSuccess);
        Assert.Contains("null", result.Error);
    }

    [Fact]
    public void ProcessOrder_ZeroQuantity_ReturnsFailed()
    {
        var order = new TradeOrder { Quantity = 0, Price = 100, Type = OrderType.Buy, CharacterTokenId = "ZZZ", TraderTelegramId = 1 };
        var result = _engine.ProcessOrder(order, new(), new(), new(), CreateToken());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ProcessOrder_ZeroPrice_ReturnsFailed()
    {
        var order = new TradeOrder { Quantity = 5, Price = 0, Type = OrderType.Buy, CharacterTokenId = "ZZZ", TraderTelegramId = 1 };
        var result = _engine.ProcessOrder(order, new(), new(), new(), CreateToken());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ProcessOrder_BuyNoMatching_ReservesBalanceAndReturnsOrder()
    {
        var buyer = CreateTrader(1, 10000m);
        var traders = new Dictionary<long, Trader> { { 1, buyer } };
        var order = CreateBuyOrder(1, quantity: 10, price: 100m);

        var result = _engine.ProcessOrder(order, new(), traders, new(), CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
        Assert.Equal(order, result.OrderToAdd);
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

        var result = _engine.ProcessOrder(order, new(), traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
        Assert.Equal(5, portfolio.ReserveQuantity);
        Assert.Equal(5, portfolio.Quantity);
        Assert.Empty(result.PortfoliosToAdd);
    }

    [Fact]
    public void ProcessOrder_SellWithoutPortfolio_ReturnsFailed()
    {
        var seller = CreateTrader(1);
        var traders = new Dictionary<long, Trader> { { 1, seller } };
        var order = CreateSellOrder(1);

        var result = _engine.ProcessOrder(order, new(), traders, new(), CreateToken());

        Assert.False(result.IsSuccess);
        Assert.Contains("портфеле", result.Error);
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

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, token);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Trades);
        Assert.Equal(5, result.Trades[0].Quantity);
        Assert.Equal(80m, result.Trades[0].Price);
        Assert.Equal(1, result.Trades[0].BuyerId);
        Assert.Equal(2, result.Trades[0].SellerId);
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

        var result = _engine.ProcessOrder(sellOrder, existingOrders, traders, portfolios, token);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Trades);
        Assert.Equal(1, result.Trades[0].BuyerId);
        Assert.Equal(2, result.Trades[0].SellerId);
        Assert.Equal(150m, result.Trades[0].Price);
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
        var token = CreateToken();

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, token);

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
        var token = CreateToken();

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, token);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Trades);
        Assert.True(portfolios.ContainsKey(1));
        Assert.Equal(5, portfolios[1].Quantity);
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
        var token = CreateToken();

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Trades.Count);
        Assert.True(buyOrder.IsFilled());
        Assert.True(sell1.IsFilled());
        Assert.Equal(2, sell2.FilledQuantity);
        Assert.Equal(1, sell2.GetRemainingQuantity());
        Assert.Contains(sell2, result.UpdatedOrders);
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

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, CreateToken());

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

        var result = _engine.ProcessOrder(order, new(), traders, new(), token);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
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

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
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

        var result = _engine.ProcessOrder(sellOrder, existingOrders, traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
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

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Trades);
        Assert.Equal(2, result.Trades[0].Quantity);
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

        var result = _engine.ProcessOrder(sellOrder, existingOrders, traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Equal(8, buyerPortfolio.Quantity);
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

        var result = _engine.ProcessOrder(buyOrder, existingOrders, traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
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

        var result = _engine.ProcessOrder(sellOrder, existingOrders, traders, portfolios, CreateToken());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
    }
}
