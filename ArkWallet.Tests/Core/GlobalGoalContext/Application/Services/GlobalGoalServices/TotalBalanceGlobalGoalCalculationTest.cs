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
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

public class TotalBalanceGlobalGoalCalculationTest
{
    private static ArkWalletDbContext CreateDb()
        => DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    [Fact]
    public void GoalName_IsTotalBalance()
    {
        Assert.Equal("Общий баланс", new TotalBalanceGlobalGoalCalculation().GoalName);
    }

    [Fact]
    public async Task CalculateAsync_SumsLatestSnapshotPerNonBotTrader()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.RegisterTrader(db, 3003);
        await HelpMethods.RegisterTrader(db, 101);
        AddSnapshot(db, traderId: 2002, totalBalance: 100m, at: new DateTime(2026, 1, 1, 10, 0, 0));
        db.BalanceSnapshots.Add(BalanceSnapshot.Create(2002, 300m, 0, 0, 0, 0, new DateTime(2026, 1, 1, 12, 0, 0)));
        AddSnapshot(db, traderId: 3003, totalBalance: 400m, at: new DateTime(2026, 1, 1, 9, 0, 0));
        AddSnapshot(db, traderId: 101, totalBalance: 9999m, at: new DateTime(2026, 1, 1, 11, 0, 0));
        await db.SaveChangesAsync();

        var sum = await new TotalBalanceGlobalGoalCalculation().CalculateAsync(db);

        Assert.Equal(700m, sum);
    }

    [Fact]
    public async Task CalculateAsync_NoSnapshots_ReturnsZero()
    {
        using var db = CreateDb();

        var sum = await new TotalBalanceGlobalGoalCalculation().CalculateAsync(db);

        Assert.Equal(0m, sum);
    }

    private static void AddSnapshot(ArkWalletDbContext db, long traderId, decimal totalBalance, DateTime at)
        => db.BalanceSnapshots.Add(BalanceSnapshot.Create(traderId, totalBalance, 0, 0, 0, 0, at));
}
