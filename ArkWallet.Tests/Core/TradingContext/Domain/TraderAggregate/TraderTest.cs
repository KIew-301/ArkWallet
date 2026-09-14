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

namespace ArkWallet.Tests.Core.TradingContext.Domain.TraderAggregate;

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
        Assert.Equal(1000m, Trader.DefaultBalance);
    }

    [Fact]
    public void CanAfford_AmountLessThanBalance_ReturnsTrue()
    {
        var trader = Trader.Create(101L, "user");

        Assert.True(trader.Balance >= 500m);
    }

    [Fact]
    public void CanAfford_AmountMoreThanBalance_ReturnsFalse()
    {
        var trader = Trader.Create(101L, "user");

        Assert.False(trader.Balance >= 2000m);
    }

    [Fact]
    public void AddToBalance_IncreasesBalance()
    {
        var trader = Trader.Create(101L, "user");

        trader.Balance += 500m;

        Assert.Equal(1500m, trader.Balance);
    }
}
