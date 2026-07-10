using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests.DomainTests;

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
    public void MarkAsFilled_SetsStatusAndExecutedAt()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        var before = DateTime.UtcNow;

        order.MarkAsFilled();

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(order.Quantity, order.FilledQuantity);
        Assert.NotNull(order.ExecutedAt);
        Assert.True(order.ExecutedAt >= before);
    }

    [Fact]
    public void UpdateOrderFill_PartialFill_UpdatesQuantityAndAveragePrice()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        order.UpdateOrderFill(5, 80m);

        Assert.Equal(5, order.FilledQuantity);
        Assert.Equal(80m, order.AverageExecutePrice);
        Assert.Equal(OrderStatus.Active, order.Status);
    }

    [Fact]
    public void UpdateOrderFill_FullFill_MarksAsFilled()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        order.UpdateOrderFill(10, 90m);

        Assert.Equal(10, order.FilledQuantity);
        Assert.Equal(90m, order.AverageExecutePrice);
        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.NotNull(order.ExecutedAt);
    }

    [Fact]
    public void UpdateOrderFill_MultipleFills_CalculatesWeightedAverage()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        order.UpdateOrderFill(3, 80m);
        order.UpdateOrderFill(2, 100m);

        Assert.Equal(5, order.FilledQuantity);
        Assert.Equal(88m, order.AverageExecutePrice);
    }

    [Fact]
    public void Cancel_WrongTrader_ThrowsDomainException()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        var ex = Assert.Throws<DomainException>(() => order.Cancel(999));

        Assert.Contains("Нельзя отменить чужой ордер", ex.Message);
    }

    [Fact]
    public void Cancel_AlreadyFilled_ThrowsDomainException()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.MarkAsFilled();

        var ex = Assert.Throws<DomainException>(() => order.Cancel(101));

        Assert.Contains("Можно отменить только активный ордер", ex.Message);
    }

    [Fact]
    public void Cancel_ValidActiveOrder_SetsCancelled()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        order.Cancel(101);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void WithQuantity_ValidQuantity_ReturnsNewOrderWithResetState()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.UpdateOrderFill(5, 80m);

        var newOrder = order.WithQuantity(3);

        Assert.NotEqual(order.Id, newOrder.Id);
        Assert.Equal(3, newOrder.Quantity);
        Assert.Equal(0, newOrder.FilledQuantity);
        Assert.Equal(100m, newOrder.Price);
        Assert.Equal(OrderType.Buy, newOrder.Type);
        Assert.Equal(101, newOrder.TraderTelegramId);
        Assert.Equal("ZZZ", newOrder.CharacterTokenId);
        Assert.Equal(OrderStatus.Active, newOrder.Status);
    }

    [Fact]
    public void WithQuantity_ZeroQuantity_ThrowsDomainException()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        var ex = Assert.Throws<DomainException>(() => order.WithQuantity(0));

        Assert.Contains("Количество должно быть больше 0", ex.Message);
    }

    [Fact]
    public void IsFilled_WhenFilled_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 5);
        order.UpdateOrderFill(5, 100m);

        Assert.True(order.IsFilled());
    }

    [Fact]
    public void IsFilled_WhenPartial_ReturnsFalse()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.UpdateOrderFill(3, 100m);

        Assert.False(order.IsFilled());
    }

    [Fact]
    public void IsActive_WhenActive_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        Assert.True(order.IsActive());
    }

    [Fact]
    public void IsActive_WhenFilled_ReturnsFalse()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.MarkAsFilled();

        Assert.False(order.IsActive());
    }

    [Fact]
    public void IsLong_BuyOrder_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        Assert.True(order.IsLong());
        Assert.False(order.IsShort());
    }

    [Fact]
    public void IsShort_SellOrder_ReturnsTrue()
    {
        var order = TradeOrder.Create(OrderType.Sell, "ZZZ", 101, 100m, 10);

        Assert.True(order.IsShort());
        Assert.False(order.IsLong());
    }

    [Fact]
    public void GetRemainingQuantity_ReturnsDifference()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.UpdateOrderFill(3, 100m);

        Assert.Equal(7, order.GetRemainingQuantity());
    }

    [Fact]
    public void GetReservedBalance_ReturnsPriceTimesRemaining()
    {
        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 50m, 10);
        order.UpdateOrderFill(4, 50m);

        Assert.Equal(300m, order.GetReservedBalance());
    }
}
