using ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;
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
using Records = global::ArkWallet.Infrastructure.Data;

namespace ArkWallet.Tests.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

public class GlobalGoalContextMapperTest
{
    [Fact]
    public void ToGoals_MapsEntitiesToAggregates()
    {
        var record = Records.GlobalGoal.Create(1, "Goal", "Desc", 1000m, 2000m, 2m, 4);
        record.Histories.Add(Records.GlobalGoalHistory.Create(1, new DateTime(2026, 1, 1), 1000m, "ZZZ", 5m));
        record.Steps.Add(Records.GlobalGoalStep.Create(1, 1, 1500m, "YYY", 10m));

        var goals = GlobalGoalContextMapper.ToGoals(new() { record });

        var goal = Assert.Single(goals);
        Assert.Equal(1, goal.Id);
        Assert.Equal("Goal", goal.Name);
        Assert.Equal(4, goal.AchievedCount);
        var history = Assert.Single(goal.History);
        Assert.Equal("ZZZ", history.SymbolForReward);
        var step = Assert.Single(goal.Steps);
        Assert.Equal(1500m, step.Target);
    }

    [Fact]
    public void ToGoals_NullCollections_DoesNotThrow()
    {
        var record = Records.GlobalGoal.Create(1, "Goal", "Desc", 1000m, 2000m, 2m, 4);
        record.Histories = null!;
        record.Steps = null!;

        var goals = GlobalGoalContextMapper.ToGoals(new() { record });

        var goal = Assert.Single(goals);
        Assert.Empty(goal.History);
        Assert.Empty(goal.Steps);
    }

    [Fact]
    public void ToGoals_EmptyList_ReturnsEmpty()
    {
        var goals = GlobalGoalContextMapper.ToGoals(new());

        Assert.Empty(goals);
    }
}
