using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.SubscriptionContext.Application.Services.SubscriptionServices;

public class SubscriptionQueryServiceTest
{
    private static SubscriptionQueryService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<SubscriptionQueryService>.Instance);

    [Fact]
    public async Task GetAllAsync_HasSubscriptions_ReturnsAll()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var subs));
        Assert.Equal(2, subs.Count);
        Assert.Contains(subs, s => s.Name == "Премиум" && s.Level == 2 && s.PriceRubles == 100 && s.MaxOrders == 20 && s.DurationMinutes == 10080);
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
        var sub = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetByIdAsync(sub.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var info));
        Assert.Equal(sub.Id, info!.Id);
        Assert.Equal("Базовая", info.Name);
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
        var sub = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var basic = await CreateService(db).GetBasicAsync();

        Assert.NotNull(basic);
        Assert.Equal("Базовая", basic!.Name);
        Assert.Null(basic.DurationMinutes);
    }
}