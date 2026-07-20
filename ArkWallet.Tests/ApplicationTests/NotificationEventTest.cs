using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ApplicationTests;

public class NotificationEventTest
{
    [Fact]
    public void FromOrderList_NullOrders_ReturnsEmptyList()
    {
        var result = NotificationEvent.FromOrderList(
            null!, new List<Trader>(), NullLogger<object>.Instance);

        Assert.Empty(result);
    }

    [Fact]
    public void FromOrderList_EmptyOrders_ReturnsEmptyList()
    {
        var result = NotificationEvent.FromOrderList(
            new List<TradeOrder>(), new List<Trader>(), NullLogger<object>.Instance);

        Assert.Empty(result);
    }

    [Fact]
    public void FromOrderList_FilledOrderWithNotificationOn_ReturnsNotification()
    {
        var trader = Trader.Create(101, "User1");
        trader.NotificationOn = true;

        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.MarkAsFilled();

        var result = NotificationEvent.FromOrderList(
            new List<TradeOrder> { order },
            new List<Trader> { trader },
            NullLogger<object>.Instance);

        Assert.Single(result);
        Assert.Equal(101, result[0].Id);
        Assert.Contains("Ордер", result[0].Message);
    }

    [Fact]
    public void FromOrderList_FilledOrderWithNotificationOff_ReturnsEmpty()
    {
        var trader = Trader.Create(101, "User1");
        trader.NotificationOn = false;

        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        order.MarkAsFilled();

        var result = NotificationEvent.FromOrderList(
            new List<TradeOrder> { order },
            new List<Trader> { trader },
            NullLogger<object>.Instance);

        Assert.Empty(result);
    }

    [Fact]
    public void FromOrderList_ActiveOrder_ReturnsEmpty()
    {
        var trader = Trader.Create(101, "User1");
        trader.NotificationOn = true;

        var order = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);

        var result = NotificationEvent.FromOrderList(
            new List<TradeOrder> { order },
            new List<Trader> { trader },
            NullLogger<object>.Instance);

        Assert.Empty(result);
    }

    [Fact]
    public void FromOrderList_MultipleOrders_ReturnsOnlyFilledWithNotification()
    {
        var trader1 = Trader.Create(101, "User1");
        trader1.NotificationOn = true;
        var trader2 = Trader.Create(102, "User2");
        trader2.NotificationOn = false;

        var filledOrder = TradeOrder.Create(OrderType.Buy, "ZZZ", 101, 100m, 10);
        filledOrder.MarkAsFilled();

        var activeOrder = TradeOrder.Create(OrderType.Sell, "ZZZ", 102, 200m, 5);

        var result = NotificationEvent.FromOrderList(
            new List<TradeOrder> { filledOrder, activeOrder },
            new List<Trader> { trader1, trader2 },
            NullLogger<object>.Instance);

        Assert.Single(result);
        Assert.Equal(101, result[0].Id);
    }
}
