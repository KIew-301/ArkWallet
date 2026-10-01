using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using Xunit;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.TradeOrderServices;

public class OrderCollectorTest
{
    [Fact]
    public void Add_Empty_Ignored()
    {
        var collector = new OrderCollector();
        collector.Add(Array.Empty<CreateOrderCommand>());
        collector.Add(null!);

        Assert.Empty(collector.TakeAll());
    }

    [Fact]
    public void Add_And_TakeAll_PreservesCollectionsInOrder()
    {
        var collector = new OrderCollector();
        var first = new[]
        {
            new CreateOrderCommand(1, "купить", "ZZZ", 10, 100m),
            new CreateOrderCommand(1, "продать", "ZZZ", 10, 110m)
        };
        var second = new[]
        {
            new CreateOrderCommand(2, "купить", "ZZZ", 20, 95m)
        };

        collector.Add(first);
        collector.Add(second);

        var taken = collector.TakeAll();

        Assert.Equal(2, taken.Count);
        Assert.Equal(first, taken[0]);
        Assert.Equal(second, taken[1]);
    }

    [Fact]
    public void TakeAll_ClearsSnapshot()
    {
        var collector = new OrderCollector();
        collector.Add(new[] { new CreateOrderCommand(1, "купить", "ZZZ", 10, 100m) });

        collector.TakeAll();

        Assert.Empty(collector.TakeAll());
    }
}