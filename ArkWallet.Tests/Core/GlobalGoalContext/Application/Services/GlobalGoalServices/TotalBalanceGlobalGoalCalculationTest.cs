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
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using System.Globalization;
using System.Numerics;

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

    /// <summary>
    /// Verify that huge balances (like 1e20) don't cause issues in the calculation pipeline.
    /// </summary>
    [Fact]
    public async Task CalculateAsync_HugeBalances_NoException()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 9901);
        await HelpMethods.RegisterTrader(db, 9902);
        db.BalanceSnapshots.Add(BalanceSnapshot.Create(9901, 1e20m, 0, 0, 0, 0, DateTime.UtcNow));
        db.BalanceSnapshots.Add(BalanceSnapshot.Create(9902, 5e20m, 0, 0, 0, 0, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var sum = await new TotalBalanceGlobalGoalCalculation().CalculateAsync(db);

        Assert.True(sum > 0);
    }

    /// <summary>
    /// Regression test for a production OverflowException. BalanceSnapshot.TotalBalance maps to
    /// an unbounded PostgreSQL numeric, so real snapshots reach 29 significant digits while each
    /// still fits System.Decimal on its own. Letting the database accumulate the sum did not:
    /// production hit 31 digits and Npgsql threw while materialising it. Each value below mirrors
    /// the highest-precision snapshot observed in production, and their unrounded total exceeds
    /// the 96-bit decimal mantissa, so this fails without rounding.
    /// </summary>
    [Fact]
    public async Task CalculateAsync_UnroundedSumWouldOverflow_StillReturnsRoundedTotal()
    {
        using var db = CreateDb();

        var now = DateTime.UtcNow;
        const decimal productionSnapshot = 110205602800.44968928076257331m;
        const int traderCount = 100;

        AssertExceedsDecimalMantissa(productionSnapshot, traderCount);
        AssertFitsDecimalMantissa(Math.Round(productionSnapshot, 4), traderCount);

        var expected = 0m;
        for (var traderId = 8001; traderId < 8001 + traderCount; traderId++)
        {
            await HelpMethods.RegisterTrader(db, traderId);
            AddSnapshot(db, traderId, productionSnapshot, now);
            expected += Math.Round(productionSnapshot, 4);
        }

        await db.SaveChangesAsync();

        var sum = await new TotalBalanceGlobalGoalCalculation().CalculateAsync(db);

        Assert.Equal(expected, sum);
    }

    private static readonly BigInteger DecimalMaxMantissa = BigInteger.Parse("79228162514264337593543950335");

    private static void AssertExceedsDecimalMantissa(decimal value, int multiplier)
    {
        var mantissa = DecimalMantissa(value) * multiplier;
        Assert.True(
            mantissa > DecimalMaxMantissa,
            $"Expected {multiplier} unrounded values to exceed the decimal mantissa, got {mantissa}.");
    }

    private static void AssertFitsDecimalMantissa(decimal value, int multiplier)
    {
        var mantissa = DecimalMantissa(value) * multiplier;
        Assert.True(
            mantissa <= DecimalMaxMantissa,
            $"Expected {multiplier} rounded values to stay within the decimal mantissa, got {mantissa}.");
    }

    private static BigInteger DecimalMantissa(decimal value)
        => BigInteger.Parse(value.ToString(CultureInfo.InvariantCulture).Replace(".", string.Empty));

    private static void AddSnapshot(ArkWalletDbContext db, long traderId, decimal totalBalance, DateTime at)
        => db.BalanceSnapshots.Add(BalanceSnapshot.Create(traderId, totalBalance, 0, 0, 0, 0, at));
}
