using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.SubscriptionContext.Application.Services.SubscriptionServices;

public class SubscriptionQueryServiceTest
{
    private static SubscriptionQueryService CreateService(ArkWalletDbContext db) =>
        new(TimeProvider.System, db, NullLogger<SubscriptionQueryService>.Instance);

    [Fact]
    public async Task GetAllAsync_HasSubscriptions_ReturnsAll()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, PriceWeekRubles = 30, PriceMonthRubles = 90, PriceYearRubles = 900, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var subs));
        Assert.Equal(2, subs.Count);
        Assert.Contains(subs, s => s.Name == "Премиум" && s.Level == 2 && s.PriceRubles == 100 && s.PriceWeekRubles == 30 && s.PriceMonthRubles == 90 && s.PriceYearRubles == 900 && s.MaxOrders == 20 && s.DurationMinutes == 10080);
    }

    [Fact]
    public async Task GetAllAsync_NoSubscriptions_ReturnsEmptyList()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var subs));
        Assert.Empty(subs);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsSubscription()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var sub = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, PriceMonthRubles = 120, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetByIdAsync(sub.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var info));
        Assert.Equal(sub.Id, info!.Id);
        Assert.Equal("Базовая", info.Name);
        Assert.Equal(120, info.PriceMonthRubles);
    }

    [Fact]
    public async Task GetByIdAsync_Nonexistent_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).GetByIdAsync(12345);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetBasicAsync_NoLevelOne_ReturnsNull()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var basic = await CreateService(db).GetBasicAsync();

        Assert.Null(basic);
    }

    [Fact]
    public async Task GetBasicAsync_HasLevelOne_ReturnsIt()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var sub = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, PriceMonthRubles = 120, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var basic = await CreateService(db).GetBasicAsync();

        Assert.NotNull(basic);
        Assert.Equal("Базовая", basic!.Name);
        Assert.Equal(120, basic.PriceMonthRubles);
        Assert.Null(basic.DurationMinutes);
    }

    [Fact]
    public async Task GetOffersForTraderAsync_NoActiveSubscription_AllAreBuy()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 3001);
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, PriceMonthRubles = 120, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, PriceWeekRubles = 30, PriceMonthRubles = 90, PriceYearRubles = 900, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetOffersForTraderAsync(3001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var offers));
        Assert.Equal(2, offers.Count);
        Assert.All(offers, o => Assert.Equal(SubscriptionOfferAction.Buy, o.Action));
    }

    [Fact]
    public async Task GetOffersForTraderAsync_ActiveLevel2_LowerHiddenCurrentRenewUpperUpgradeWithBonus()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 3002);

        var lvl1 = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, PriceMonthRubles = 120, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        var lvl2 = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, PriceWeekRubles = 30, PriceMonthRubles = 90, PriceYearRubles = 900, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        var lvl3 = new Subscription { Name = "VIP", Level = 3, PriceRubles = 250, PriceWeekRubles = 70, PriceMonthRubles = 180, PriceYearRubles = 1800, MaxOrders = 100, MaxMiningMachines = 50, DurationMinutes = 43200 };
        db.Subscriptions.Add(lvl1);
        db.Subscriptions.Add(lvl2);
        db.Subscriptions.Add(lvl3);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 3002L);
        trader.SubscriptionId = lvl2.Id;
        trader.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(10);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetOffersForTraderAsync(3002);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var offers));

        // Уровень 1 ниже активного (2) — скрыт
        Assert.DoesNotContain(offers, o => o.Level == 1);

        // Уровень 2 = активный → продление
        var current = Assert.Single(offers, o => o.Level == 2);
        Assert.Equal(SubscriptionOfferAction.Renew, current.Action);
        Assert.Null(current.BonusMinutes);

        // Уровень 3 выше → улучшение с бонусом
        var upper = Assert.Single(offers, o => o.Level == 3);
        Assert.Equal(SubscriptionOfferAction.Upgrade, upper.Action);
        Assert.NotNull(upper.BonusMinutes);
        Assert.True(upper.BonusMinutes > 0);
    }
}