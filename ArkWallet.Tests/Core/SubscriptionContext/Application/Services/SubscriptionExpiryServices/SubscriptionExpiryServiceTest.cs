using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;

public class SubscriptionExpiryServiceTest
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SubscriptionExpiryService CreateService(ArkWalletDbContext db, TimeProvider timeProvider)
    {
        var mediator = new Mock<MediatR.IMediator>();
        var eventPublisher = new MediatREventPublisher(mediator.Object);
        return new(db, eventPublisher, timeProvider, NullLogger<SubscriptionExpiryService>.Instance);
    }

    private static TestTimeProvider CreateTimeProvider() => new TestTimeProvider();

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

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(1, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
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

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(0, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
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

        var trader1 = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader1.SubscriptionId = 1;
        trader1.SubscriptionExpiresAtUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var trader2 = await db.Traders.FirstAsync(t => t.TelegramId == 2001);
        trader2.SubscriptionId = 1;
        trader2.SubscriptionExpiresAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var trader3 = await db.Traders.FirstAsync(t => t.TelegramId == 2002);
        trader3.SubscriptionId = 1;
        trader3.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var next = await CreateService(db, CreateTimeProvider()).GetNextExpiryAsync();

        Assert.NotNull(next);
        Assert.Equal(2001, next.TraderId);
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), next.ExpiresAtUtc);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_ExpiredTrader_MovesToBasic()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(2000);

        Assert.Equal(1, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(basic.Id, updated.SubscriptionId);
        Assert.Null(updated.SubscriptionExpiresAtUtc);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_FutureExpiry_DoesNotTouch()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(2000);

        Assert.Equal(0, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(premium.Id, updated.SubscriptionId);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_NoBasicSubscription_ReturnsZero()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = 1;
        trader.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(2000);

        Assert.Equal(0, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(1, updated.SubscriptionId);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_UnknownTrader_ReturnsZero()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.Subscriptions.Add(new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null });
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(9999);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_Multiple_OnlyExpiredMoved()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        await HelpMethods.RegisterTrader(db, 2001);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader1 = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader1.SubscriptionId = premium.Id;
        trader1.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var trader2 = await db.Traders.FirstAsync(t => t.TelegramId == 2001);
        trader2.SubscriptionId = premium.Id;
        trader2.SubscriptionExpiresAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(new long[] { 2000, 2001 });

        Assert.Equal(1, result);
        var updated1 = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(basic.Id, updated1.SubscriptionId);
        Assert.Null(updated1.SubscriptionExpiresAtUtc);
        var updated2 = await db.Traders.FirstAsync(t => t.TelegramId == 2001);
        Assert.Equal(premium.Id, updated2.SubscriptionId);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_EmptyArray_ReturnsZero()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(Array.Empty<long>());

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_RenewedAfterNextExpiryRead_DoesNotTouch()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Basic", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Premium", Level = 2, PriceRubles = 500, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 100 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var service = CreateService(db, CreateTimeProvider());

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000L);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = NowUtc.AddMinutes(1);
        await db.SaveChangesAsync();

        var next = await service.GetNextExpiryAsync();

        Assert.NotNull(next);
        Assert.Equal(2000L, next.TraderId);

        // Симулируем продление (race condition): трейдер обновил подписку пока воркер "спал"
        trader.SubscriptionExpiresAtUtc = NowUtc.AddMinutes(60);
        await db.SaveChangesAsync();

        var moved = await service.DowngradeToBasicAsync(2000);

        Assert.Equal(0, moved);
        db.ChangeTracker.Clear();
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000L);
        Assert.Equal(premium.Id, updated.SubscriptionId);
        Assert.Equal(NowUtc.AddMinutes(60), updated.SubscriptionExpiresAtUtc);
    }

    [Fact]
    public async Task ProcessExpiredAsync_ExpiredTrader_CreatesHistoryEntry()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(1, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(basic.Id, updated.SubscriptionId);
        Assert.Null(updated.SubscriptionExpiresAtUtc);
        var entry = await db.SubscriptionPurchaseHistory.SingleAsync(h => h.TraderId == 2000);
        Assert.Equal(basic.Id, entry.SubscriptionId);
        Assert.Equal(0, entry.PriceRubles);
        Assert.Null(entry.ExpiresAtUtc);
        Assert.Null(entry.TransactionId);
    }

    [Fact]
    public async Task ProcessExpiredAsync_FutureExpiry_DoesNotCreateHistory()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).ProcessExpiredAsync();

        Assert.Equal(0, result);
        var count = await db.SubscriptionPurchaseHistory.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_ExpiredTrader_CreatesHistoryEntry()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader.SubscriptionId = premium.Id;
        trader.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(2000);

        Assert.Equal(1, result);
        var updated = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(basic.Id, updated.SubscriptionId);
        Assert.Null(updated.SubscriptionExpiresAtUtc);
        var entry = await db.SubscriptionPurchaseHistory.SingleAsync(h => h.TraderId == 2000);
        Assert.Equal(basic.Id, entry.SubscriptionId);
        Assert.Equal(0, entry.PriceRubles);
        Assert.Null(entry.ExpiresAtUtc);
        Assert.Null(entry.TransactionId);
    }

    [Fact]
    public async Task DowngradeToBasicAsync_Multiple_OnlyExpiredCreatesHistory()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        await HelpMethods.RegisterTrader(db, 2001);
        var basic = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(basic);
        var premium = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        var trader1 = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        trader1.SubscriptionId = premium.Id;
        trader1.SubscriptionExpiresAtUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var trader2 = await db.Traders.FirstAsync(t => t.TelegramId == 2001);
        trader2.SubscriptionId = premium.Id;
        trader2.SubscriptionExpiresAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var result = await CreateService(db, CreateTimeProvider()).DowngradeToBasicAsync(new long[] { 2000, 2001 });

        Assert.Equal(1, result);
        var updated1 = await db.Traders.FirstAsync(t => t.TelegramId == 2000);
        Assert.Equal(basic.Id, updated1.SubscriptionId);
        Assert.Null(updated1.SubscriptionExpiresAtUtc);
        var updated2 = await db.Traders.FirstAsync(t => t.TelegramId == 2001);
        Assert.Equal(premium.Id, updated2.SubscriptionId);
        var entry = await db.SubscriptionPurchaseHistory.SingleAsync(h => h.TraderId == 2000);
        Assert.Equal(basic.Id, entry.SubscriptionId);
        Assert.Null(entry.ExpiresAtUtc);
    }
}
