using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
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
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Tests.Core.TradingContext.Domain.MarketMakerAggregate;

public class MarketMakerBotTest
{
    [Fact]
    public void Create_ValidData_ReturnsBot()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer, 75);

        Assert.Equal(101, bot.TraderId);
        Assert.Equal("ARK_001", bot.Symbol);
        Assert.Equal(BotRole.Buyer, bot.Role);
        Assert.Equal(75, bot.BasePower);
        Assert.True(bot.IsActive);
        Assert.True(bot.NextPowerChange > DateTime.UtcNow);
    }

    [Fact]
    public void Create_DefaultPower_Returns50()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Seller);

        Assert.Equal(50, bot.BasePower);
        Assert.Equal(BotRole.Seller, bot.Role);
    }

    [Fact]
    public void Create_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer);
        var after = DateTime.UtcNow;

        Assert.True(bot.CreatedAt >= before);
        Assert.True(bot.CreatedAt <= after);
    }

    [Fact]
    public void SetRole_ChangesRole()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer);

        bot.Role = BotRole.Seller;

        Assert.Equal(BotRole.Seller, bot.Role);
    }

    [Fact]
    public void SetActive_ChangesActiveState()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer);

        bot.IsActive = false;

        Assert.False(bot.IsActive);
    }

    [Fact]
    public void SetBasePower_ChangesPower()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer);

        bot.BasePower = 200;

        Assert.Equal(200, bot.BasePower);
    }

    [Fact]
    public void UpdatePower_ClampsWithinBounds()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer, 50);

        for (int i = 0; i < 100; i++)
        {
            var change = Random.Shared.Next(-35, 35);
            bot.BasePower = Math.Clamp(bot.BasePower + change, 10, 100);
            bot.NextPowerChange = DateTime.UtcNow.AddMinutes(Random.Shared.Next(2, 5));
        }

        Assert.InRange(bot.BasePower, 10, 100);
        Assert.True(bot.NextPowerChange > DateTime.UtcNow);
    }

    [Fact]
    public void UpdateRebalanced_SetsNextRebalance()
    {
        var bot = MarketMakerBotRecord.Create(101, "ARK_001", BotRole.Buyer);
        var before = DateTime.UtcNow;

        bot.NextRebalance = DateTime.UtcNow.AddMinutes(10);

        Assert.True(bot.NextRebalance > before);
    }

    [Fact]
    public void MoveToTrader_ReturnsPreviousTraderId()
    {
        var bot = MarketMakerBot.Create(42, "ARK_001", ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Buyer);
        long previous = bot.MoveToTrader(99);

        Assert.Equal(42, previous);
        Assert.Equal(99, bot.TraderId);
    }

    [Fact]
    public void MoveToTrader_SameTrader_ReturnsSameAndKeeps()
    {
        var bot = MarketMakerBot.Create(55, "ARK_001", ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Seller);
        long first = bot.MoveToTrader(55);
        long second = bot.MoveToTrader(55);

        Assert.Equal(55, first);
        Assert.Equal(55, second);
        Assert.Equal(55, bot.TraderId);
    }

    [Fact]
    public void MoveToTrader_InvalidTraderId_Throws()
    {
        var bot = MarketMakerBot.Create(10, "ARK_001", ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Waller);

        Assert.Throws<ArgumentOutOfRangeException>(() => bot.MoveToTrader(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => bot.MoveToTrader(-7));
    }
}
