using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Tests.HelpTools;
using Xunit;

namespace ArkWallet.Tests.Core.TradingContext.Domain.TraderAggregate;

public class OrderTest
{
    private readonly RecordingEventPublisher _publisher = new();

    private static Order CreateOrder(OrderType type = OrderType.Buy, string symbol = "ZZZ", decimal price = 100m, int quantity = 10)
        => Order.Create(type, symbol, price, quantity);

    [Fact]
    public async Task UpdateOrderFill_MultipleFills_CalculatesWeightedAverage()
    {
        var order = Order.Create(OrderType.Buy, "ZZZ", 100m, 10);
        order.SetEventPublisher(_publisher);

        await order.UpdateOrderFill(3, 80m);
        await order.UpdateOrderFill(2, 100m);

        Assert.Equal(5, order.FilledQuantity);
        Assert.Equal(88m, order.AverageExecutePrice);
    }

    [Fact]
    public async Task UpdateOrderFill_ExceedsRemaining_ThrowsDomainException()
    {
        var order = Order.Create(OrderType.Buy, "ZZZ", 100m, 5);
        order.SetEventPublisher(_publisher);

        var ex = await Assert.ThrowsAsync<DomainException>(() => order.UpdateOrderFill(6, 80m));

        Assert.Contains("exceeds remaining", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithQuantity_ValidQuantity_ReturnsNewOrder()
    {
        var order = Order.Create(OrderType.Buy, "ZZZ", 100m, 10);

        var newOrder = order.WithQuantity(3);

        Assert.NotEqual(order.Id, newOrder.Id);
        Assert.Equal(3, newOrder.Quantity);
        Assert.Equal(order.Price, newOrder.Price);
    }

    [Fact]
    public void Cancel_ActiveOrder_SetsCancelled()
    {
        var order = Order.Create(OrderType.Buy, "ZZZ", 100m, 10);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_NotActive_ThrowsDomainException()
    {
        var order = Order.Reconstruct(new OrderLoadCommand(
            Id: "test-id",
            Type: OrderType.Buy,
            CharacterTokenId: "ZZZ",
            Price: 100m,
            Quantity: 10,
            FilledQuantity: 10,
            Status: OrderStatus.Filled,
            CreatedAt: DateTime.UtcNow,
            FilledAt: DateTime.UtcNow));

        var ex = Assert.Throws<DomainException>(() => order.Cancel());

        Assert.Contains("Only active orders can be cancelled", ex.Message);
    }
}
