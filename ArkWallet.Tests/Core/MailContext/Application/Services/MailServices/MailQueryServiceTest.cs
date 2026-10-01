using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.MailContext.Application.Services.MailServices;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.MailContext.Application.Services.MailServices;

public class MailQueryServiceTest
{
    private static ArkWalletDbContext CreateDb()
        => DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    [Fact]
    public async Task GetUserMailsAsync_ReturnsOnlyTradersMailsOrderedByCreatedAtDesc()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.RegisterTrader(db, 3003);
        db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
            2002, "Old", "m", "Admin", null, "", 0,
            new DateTime(2026, 1, 1, 8, 0, 0))));
        db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
            2002, "New", "m", "Admin", null, "ZZZ", 5,
            new DateTime(2026, 1, 1, 9, 0, 0))));
        db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
            3003, "Other", "m", "Admin", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0))));
        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsAsync(2002);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var mails));
        Assert.Equal(2, mails!.Count);
        Assert.Equal("New", mails[0].Title);
        Assert.Equal("Old", mails[1].Title);
        Assert.Equal("ZZZ", mails[0].SymbolForReward);
    }

    [Fact]
    public async Task GetUserMailsAsync_NoMails_ReturnsEmpty()
    {
        using var db = CreateDb();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsAsync(2002);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var mails));
        Assert.Empty(mails!);
    }

    [Fact]
    public async Task GetUserMailsPageAsync_ReturnsRequestedPageWithTotalMetadata()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.RegisterTrader(db, 3003);

        for (int i = 0; i < 25; i++)
        {
            db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
                2002, $"Mail {i}", "msg", "System", null, "", 0,
                new DateTime(2026, 1, 1, 10, 0, 0).AddMinutes(i))));
        }
        db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
            3003, "Other", "m", "Admin", null, "", 0,
            new DateTime(2026, 1, 1, 11, 0, 0))));
        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsPageAsync(2002, MailFilter.All, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var page));
        Assert.NotNull(page);
        Assert.Equal(20, page!.Items.Count);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(2, page.TotalPages);
        Assert.True(page.HasNext);
        Assert.False(page.HasPrevious);
    }

    [Fact]
    public async Task GetUserMailsPageAsync_SecondPageReturnsRemainder()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        for (int i = 0; i < 25; i++)
        {
            db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
                2002, $"Mail {i}", "msg", "System", null, "", 0,
                new DateTime(2026, 1, 1, 10, 0, 0).AddMinutes(i))));
        }
        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsPageAsync(2002, MailFilter.All, 2, 20);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var page));
        Assert.NotNull(page);
        Assert.Equal(5, page!.Items.Count);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(2, page.TotalPages);
        Assert.False(page.HasNext);
        Assert.True(page.HasPrevious);
    }

    [Fact]
    public async Task GetUserMailsPageAsync_UnreadFilterReturnsOnlySentMails()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        var sentMail = MailMessage.Create(new MailMessageDraft(
            2002, "SentMail", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        sentMail.Status = "Sent";
        db.MailMessages.Add(sentMail);

        var readMail = MailMessage.Create(new MailMessageDraft(
            2002, "ReadMail", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        readMail.Status = "Read";
        readMail.ReadAt = new DateTime(2026, 1, 1, 11, 0, 0);
        db.MailMessages.Add(readMail);

        var acceptMail = MailMessage.Create(new MailMessageDraft(
            2002, "AcceptMail", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        acceptMail.Status = "Accepted";
        acceptMail.AcceptedAt = new DateTime(2026, 1, 1, 12, 0, 0);
        db.MailMessages.Add(acceptMail);

        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsPageAsync(2002, MailFilter.Unread, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var page));
        Assert.NotNull(page);
        Assert.Single(page!.Items);
        Assert.Equal("SentMail", page.Items[0].Title);
    }

    [Fact]
    public async Task GetUserMailsPageAsync_RewardFilterReturnsOnlyMailsWithAvailableReward()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        var r1 = MailMessage.Create(new MailMessageDraft(
            2002, "R1", "m", "S", null, "ZZZ", 5,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        r1.Status = "Sent";
        db.MailMessages.Add(r1);

        var r2 = MailMessage.Create(new MailMessageDraft(
            2002, "R2", "m", "S", null, "XXX", 3,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        r2.Status = "Read";
        db.MailMessages.Add(r2);

        var r3 = MailMessage.Create(new MailMessageDraft(
            2002, "R3", "m", "S", null, "YYY", 7,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        r3.Status = "Accepted";
        db.MailMessages.Add(r3);

        var n1 = MailMessage.Create(new MailMessageDraft(
            2002, "N1", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        n1.Status = "Sent";
        db.MailMessages.Add(n1);

        var n2 = MailMessage.Create(new MailMessageDraft(
            2002, "N2", "m", "S", null, "QQQ", 0,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        n2.Status = "Sent";
        db.MailMessages.Add(n2);

        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsPageAsync(2002, MailFilter.Reward, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var page));
        Assert.NotNull(page);
        Assert.Equal(2, page!.Items.Count);
        Assert.Contains(page.Items, m => m.SymbolForReward == "ZZZ");
        Assert.Contains(page.Items, m => m.SymbolForReward == "XXX");
    }

    [Fact]
    public async Task GetUserMailsPageAsync_OrdersByIdDescWhenCreatedAtEqual()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        var baseTime = new DateTime(2026, 1, 1, 10, 0, 0);
        for (int i = 1; i <= 3; i++)
        {
            db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
                2002, $"Mail{i}", "m", "S", null, "", 0,
                baseTime)));
        }
        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetUserMailsPageAsync(2002, MailFilter.All, 1, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var page));
        Assert.NotNull(page);
        Assert.Equal(3, page!.Items.Count);
        Assert.Equal(3, page.Items[0].Id);
        Assert.Equal(2, page.Items[1].Id);
        Assert.Equal(1, page.Items[2].Id);
    }

    [Fact]
    public async Task GetMailCountersAsync_ReturnsUnreadRewardAndTotalCounts()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        var u1 = MailMessage.Create(new MailMessageDraft(
            2002, "U1", "m", "S", null, "ZZZ", 5,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        u1.Status = "Sent";
        db.MailMessages.Add(u1);

        var u2 = MailMessage.Create(new MailMessageDraft(
            2002, "U2", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        u2.Status = "Sent";
        db.MailMessages.Add(u2);

        var r1 = MailMessage.Create(new MailMessageDraft(
            2002, "R1", "m", "S", null, "XXX", 3,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        r1.Status = "Read";
        r1.ReadAt = new DateTime(2026, 1, 1, 11, 0, 0);
        db.MailMessages.Add(r1);

        var a1 = MailMessage.Create(new MailMessageDraft(
            2002, "A1", "m", "S", null, "YYY", 7,
            new DateTime(2026, 1, 1, 10, 0, 0)));
        a1.Status = "Accepted";
        a1.AcceptedAt = new DateTime(2026, 1, 1, 12, 0, 0);
        db.MailMessages.Add(a1);

        await db.SaveChangesAsync();

        var service = new QueryService(db, NullLogger<QueryService>.Instance);
        var result = await service.GetMailCountersAsync(2002);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var counters));
        Assert.NotNull(counters);
        Assert.Equal(2, counters!.UnreadCount);
        Assert.Equal(2, counters.RewardCount);
        Assert.Equal(4, counters.TotalCount);
    }

    [Fact]
    public async Task GetUserMailAsync_ReturnsMailOfTrader()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);
        await HelpMethods.RegisterTrader(db, 3003);

        db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
            2002, "OwnMail", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0))));
        db.MailMessages.Add(MailMessage.Create(new MailMessageDraft(
            3003, "OtherMail", "m", "S", null, "", 0,
            new DateTime(2026, 1, 1, 10, 0, 0))));
        await db.SaveChangesAsync();

        var ownMailId = db.MailMessages.First(m => m.Title == "OwnMail").Id;
        var otherMailId = db.MailMessages.First(m => m.Title == "OtherMail").Id;

        var service = new QueryService(db, NullLogger<QueryService>.Instance);

        var result1 = await service.GetUserMailAsync(2002, ownMailId);
        Assert.True(result1.IsSuccess);
        Assert.True(result1.TryGetData(out var mail1));
        Assert.NotNull(mail1);
        Assert.Equal(ownMailId, mail1!.Id);

        var result2 = await service.GetUserMailAsync(2002, otherMailId);
        Assert.False(result2.IsSuccess);
    }

}
