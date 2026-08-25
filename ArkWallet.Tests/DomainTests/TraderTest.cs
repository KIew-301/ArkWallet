using ArkWallet.Domain.Entities;

namespace ArkWallet.Tests.DomainTests;

public class TraderTest
{
    [Fact]
    public void Create_ValidData_ReturnsTrader()
    {
        var trader = Trader.Create(101L, "testuser");

        Assert.Equal(101L, trader.TelegramId);
        Assert.Equal("testuser", trader.Username);
        Assert.Equal(1000m, trader.Balance);
        Assert.True(trader.NotificationOn);
    }

    [Fact]
    public void Create_NullUsername_ReturnsTrader()
    {
        var trader = Trader.Create(101L, null);

        Assert.Null(trader.Username);
    }

    [Fact]
    public void Create_SetsJoinedAt()
    {
        var before = DateTime.UtcNow;
        var trader = Trader.Create(101L, "user");

        Assert.True(trader.JoinedAt >= before);
    }

    [Fact]
    public void GetDefaultBalance_Returns1000()
    {
        Assert.Equal(1000m, Trader.GetDefaultBalance());
    }

    [Fact]
    public void CanAfford_AmountLessThanBalance_ReturnsTrue()
    {
        var trader = Trader.Create(101L, "user");

        Assert.True(trader.CanAfford(500m));
    }

    [Fact]
    public void CanAfford_AmountMoreThanBalance_ReturnsFalse()
    {
        var trader = Trader.Create(101L, "user");

        Assert.False(trader.CanAfford(2000m));
    }

    [Fact]
    public void AddToBalance_IncreasesBalance()
    {
        var trader = Trader.Create(101L, "user");

        trader.AddToBalance(500m);

        Assert.Equal(1500m, trader.Balance);
    }
}
