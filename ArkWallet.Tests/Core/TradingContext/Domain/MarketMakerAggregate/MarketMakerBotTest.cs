using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
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
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer, 75);

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
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Seller);

        Assert.Equal(50, bot.BasePower);
        Assert.Equal(BotRole.Seller, bot.Role);
    }

    [Fact]
    public void Create_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);
        var after = DateTime.UtcNow;

        Assert.True(bot.CreatedAt >= before);
        Assert.True(bot.CreatedAt <= after);
    }

    [Fact]
    public void SetRole_ChangesRole()
    {
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);

        bot.SetRole(BotRole.Seller);

        Assert.Equal(BotRole.Seller, bot.Role);
    }

    [Fact]
    public void SetActive_ChangesActiveState()
    {
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);

        bot.SetActive(false);

        Assert.False(bot.IsActive);
    }

    [Fact]
    public void SetBasePower_ChangesPower()
    {
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);

        bot.SetBasePower(200);

        Assert.Equal(200, bot.BasePower);
    }

    [Fact]
    public void UpdatePower_ClampsWithinBounds()
    {
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer, 50);

        for (int i = 0; i < 100; i++)
            bot.UpdatePower(10, 100);

        Assert.InRange(bot.BasePower, 10, 100);
        Assert.True(bot.NextPowerChange > DateTime.UtcNow);
    }

    [Fact]
    public void UpdateRebalanced_SetsNextRebalance()
    {
        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);
        var before = DateTime.UtcNow;

        bot.UpdateRebalanced();

        Assert.True(bot.NextRebalance > before);
    }
}
