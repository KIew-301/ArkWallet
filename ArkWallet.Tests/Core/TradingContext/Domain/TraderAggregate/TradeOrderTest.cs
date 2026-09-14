using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.General.Domain.ValueObjects;

namespace ArkWallet.Tests.Core.TradingContext.Domain.TraderAggregate;

public class TradeOrderTest
{
    [Fact]
    public void Create_ValidData_ReturnsActiveOrder()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        Assert.Equal(OrderType.Buy, order.Type);
        Assert.Equal("ZZZ", order.CharacterTokenId);
        Assert.Equal(101, order.TraderTelegramId);
        Assert.Equal(100m, order.Price);
        Assert.Equal(10, order.Quantity);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Equal(0, order.FilledQuantity);
        Assert.Equal(0m, order.AverageExecutePrice);
    }

    [Fact]
    public void Create_ZeroPrice_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 0m, 10));

        Assert.Contains("Цена должна быть больше 0", ex.Message);
    }

    [Fact]
    public void Create_NegativePrice_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TradeOrder.Create(OrderType.Sell, "ZZZ", 101, -5m, 10));

        Assert.Contains("Цена должна быть больше 0", ex.Message);
    }

    [Fact]
    public void Create_ZeroQuantity_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 0));

        Assert.Contains("Количество токенов должно быть больше 0", ex.Message);
    }

    [Fact]
    public void Create_NegativeQuantity_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, -5));

        Assert.Contains("Количество токенов должно быть больше 0", ex.Message);
    }

    [Fact]
    public void Update_WrongTrader_ThrowsDomainException()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        var ex = Assert.Throws<DomainException>(() => order.Update(999));

        Assert.Contains("Нельзя отменить чужой ордер", ex.Message);
    }

    [Fact]
    public void Update_AlreadyFilled_ThrowsDomainException()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.Status = OrderStatus.Filled;
        order.ExecutedAt = DateTime.UtcNow;
        order.FilledQuantity = order.Quantity;

        var ex = Assert.Throws<DomainException>(() => order.Update(101));

        Assert.Contains("Можно отменить только активный ордер", ex.Message);
    }

    [Fact]
    public void Update_ValidActiveOrder_SetsCancelled()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        order.Update(101);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void IsActive_WhenActive_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        Assert.True(order.IsActive);
    }

    [Fact]
    public void IsActive_WhenFilled_ReturnsFalse()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.Status = OrderStatus.Filled;
        order.ExecutedAt = DateTime.UtcNow;
        order.FilledQuantity = order.Quantity;

        Assert.False(order.IsActive);
    }

    [Fact]
    public void IsLong_BuyOrder_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        Assert.True(order.IsLong);
        Assert.False(order.IsShort);
    }

    [Fact]
    public void IsShort_SellOrder_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Sell, "ZZZ", 101, 100m, 10);

        Assert.True(order.IsShort);
        Assert.False(order.IsLong);
    }

}
