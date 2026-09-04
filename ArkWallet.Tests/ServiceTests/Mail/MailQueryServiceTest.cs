using ArkWallet.Application.Services.MailServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mail;

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

        var service = new MailQueryService(db, NullLogger<MailQueryService>.Instance);
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

        var service = new MailQueryService(db, NullLogger<MailQueryService>.Instance);
        var result = await service.GetUserMailsAsync(2002);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var mails));
        Assert.Empty(mails!);
    }
}
