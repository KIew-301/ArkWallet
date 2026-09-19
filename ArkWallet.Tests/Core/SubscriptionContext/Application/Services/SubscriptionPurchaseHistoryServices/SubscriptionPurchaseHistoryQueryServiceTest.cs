using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseHistoryServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseHistoryServices;

public class SubscriptionPurchaseHistoryQueryServiceTest
{
    private static SubscriptionPurchaseHistoryQueryService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<SubscriptionPurchaseHistoryQueryService>.Instance);

    [Fact]
    public async Task GetHistoryAsync_HasEntries_ReturnsDescOrderedWithSubscriptionName()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);

        var sub = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.SubscriptionPurchaseHistory.Add(new SubscriptionPurchaseHistory
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            PriceRubles = 100,
            PurchasedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            TransactionId = "TXN-1"
        });
        db.SubscriptionPurchaseHistory.Add(new SubscriptionPurchaseHistory
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            PriceRubles = 100,
            PurchasedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc = new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc),
            TransactionId = "TXN-2"
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync(2000);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var entries));
        Assert.Equal(2, entries.Count);
        Assert.Equal("TXN-2", entries[0].TransactionId);
        Assert.Equal("TXN-1", entries[1].TransactionId);
        Assert.Equal("Премиум", entries[0].SubscriptionName);
    }

    [Fact]
    public async Task GetHistoryAsync_NoEntries_ReturnsEmpty()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);

        var result = await CreateService(db).GetHistoryAsync(2000);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var entries));
        Assert.Empty(entries);
    }
}