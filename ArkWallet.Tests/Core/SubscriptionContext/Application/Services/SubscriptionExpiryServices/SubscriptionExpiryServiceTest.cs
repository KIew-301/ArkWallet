using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;

public class SubscriptionExpiryServiceTest
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SubscriptionExpiryService CreateService(ArkWalletDbContext db, TimeProvider timeProvider) =>
        new(db, timeProvider, NullLogger<SubscriptionExpiryService>.Instance);

    private static TimeProvider CreateTimeProvider() => new TestTimeProvider();

    [Fact]
    public async Task ProcessExpiredAsync_NoBasicSubscription_ReturnsZero()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ProcessExpiredAsync_ExpiredTrader_MovesToBasic()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(1, result);
        var updated = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        Assert.Equal(basic.Id, updated.SubscriptionId);
        Assert.Null(updated.SubscriptionExpiresAtUtc);
    }

    [Fact]
    public async Task ProcessExpiredAsync_FutureExpiry_NotMoved()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(0, result);
        var updated = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        Assert.Equal(premium.Id, updated.SubscriptionId);
    }

    [Fact]
    public async Task ProcessExpiredAsync_NoExpiredTraders_ReturnsZero()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetNextExpiryAsync_NoFutureExpiry_ReturnsNull()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        await db.SaveChangesAsync();

        var next = await CreateService(db, CreateTimeProvider()).GetNextExpiryAsync();

        Assert.Null(next);
    }

    [Fact]
    public async Task GetNextExpiryAsync_HasFutureExpiries_ReturnsMinimum()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        await HelpMethods.RegisterTrader(db, 2001);
        await HelpMethods.RegisterTrader(db, 2002);
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        await db.SaveChangesAsync();

        var trader1 = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        trader1.SubscriptionId = 1;
        trader1.SubscriptionExpiresAtUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var trader2 = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2001);
        trader2.SubscriptionId = 1;
        trader2.SubscriptionExpiresAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var trader3 = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2002);
        trader3.SubscriptionId = 1;
        trader3.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var next = await CreateService(db, CreateTimeProvider()).GetNextExpiryAsync();

        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), next);
    }
}
