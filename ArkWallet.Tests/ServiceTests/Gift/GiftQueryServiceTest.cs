using ArkWallet.Application.Services.GiftServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Records = ArkWallet.Domain.Entities;

namespace ArkWallet.Tests.ServiceTests.Gift;

public class GiftQueryServiceTest
{
    private static readonly string[] ExpectedTokenOrder = new[] { "AAA", "BBB", "ZZZ" };

    private static GiftQueryService CreateService(ArkWalletDbContext db)
        => new(db, NullLogger<GiftQueryService>.Instance);

    private static Records.Gift CreatePendingGift(Guid id, long senderId, long recipientId, string symbol, decimal quantity, DateTime sentAt)
        => Records.Gift.Create(id, senderId, recipientId, symbol, quantity, 100m, sentAt);

    [Fact]
    public async Task GetPendingGifts_ReturnsOnlySentForRecipient()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var now = DateTime.UtcNow;
        var zzzGift = CreatePendingGift(Guid.NewGuid(), 1001, 2001, "ZZZ", 2, now);
        var arkGift = CreatePendingGift(Guid.NewGuid(), 1002, 2001, "ARK_001", 5, now);
        var otherRecipient = CreatePendingGift(Guid.NewGuid(), 1001, 3001, "ZZZ", 3, now);
        var aaaGift = CreatePendingGift(Guid.NewGuid(), 1001, 2001, "AAA", 1, now);
        db.Gifts.AddRange(zzzGift, arkGift, otherRecipient, aaaGift);
        await db.SaveChangesAsync();

        zzzGift.MarkAsReceived(now);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetPendingGiftsAsync(2001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var gifts));
        Assert.Equal(2, gifts.Count);
        Assert.Contains(gifts, g => g.TokenSymbol == "ARK_001");
        Assert.Contains(gifts, g => g.TokenSymbol == "AAA");
        Assert.DoesNotContain(gifts, g => g.TokenSymbol == "ZZZ");
    }

    [Fact]
    public async Task GetPendingGifts_NoGifts_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = CreateService(db);
        var result = await service.GetPendingGiftsAsync(2001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var gifts));
        Assert.NotNull(gifts);
        Assert.Empty(gifts);
    }

    [Fact]
    public async Task GetPendingGifts_OrdersBySentAtAscending()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var g1 = CreatePendingGift(Guid.NewGuid(), 1001, 2001, "ZZZ", 1, baseTime.AddHours(2));
        var g2 = CreatePendingGift(Guid.NewGuid(), 1001, 2001, "AAA", 1, baseTime);
        var g3 = CreatePendingGift(Guid.NewGuid(), 1001, 2001, "BBB", 1, baseTime.AddHours(1));
        db.Gifts.AddRange(g1, g2, g3);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetPendingGiftsAsync(2001);

        Assert.True(result.TryGetData(out var gifts));
        Assert.Equal(ExpectedTokenOrder, gifts.Select(g => g.TokenSymbol).ToArray());
    }
}
