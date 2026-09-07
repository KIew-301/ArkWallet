using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Services.MailServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Mail;

public class MailMessageServiceTest
{
    private static ArkWalletDbContext CreateDb() =>
        DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    private static MailMessageService BuildService(
        ArkWalletDbContext db,
        ITaskDispatcher? dispatcher = null,
        TestTimeProvider? timeProvider = null)
        => new MailMessageService(
            db,
            dispatcher ?? new Mock<ITaskDispatcher>().Object,
            NullLogger<MailMessageService>.Instance,
            timeProvider ?? new TestTimeProvider());

    [Fact]
    public async Task CreateAsync_DefaultType_CreatesNotificationMail()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.CreateAsync(new MailCreateCommand(
            TraderId: 2002, Title: "T", Message: "M", SenderName: "S", SenderId: null,
            SymbolForReward: "", AmountForReward: 0));

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Id > 0);
        var mail = db.MailMessages.Single();
        Assert.Equal(2002, mail.TraderId);
        Assert.Equal(MailType.Notification.ToString(), mail.Type);
        Assert.Equal(MailMessageStatus.Sent.ToString(), mail.Status);
    }

    [Fact]
    public async Task CreateAsync_WithRewardType_PersistsType()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.CreateAsync(new MailCreateCommand(
            TraderId: 2002, Title: "T", Message: "M", SenderName: "S", SenderId: null,
            SymbolForReward: "ZZZ", AmountForReward: 5, Type: "Reward"));

        Assert.True(result.IsSuccess);
        var mail = db.MailMessages.Single();
        Assert.Equal(MailType.Reward.ToString(), mail.Type);
        Assert.Equal("ZZZ", mail.SymbolForReward);
        Assert.Equal(5, mail.AmountForReward);
    }

    [Fact]
    public async Task CreateAsync_UnknownType_FallsBackToNotification()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.CreateAsync(new MailCreateCommand(
            TraderId: 2002, Title: "T", Message: "M", SenderName: "S", SenderId: null,
            SymbolForReward: "", AmountForReward: 0, Type: "Bogus"));

        Assert.True(result.IsSuccess);
        var mail = db.MailMessages.Single();
        Assert.Equal(MailType.Notification.ToString(), mail.Type);
    }

    [Fact]
    public async Task CreateAsync_NotificationOn_DispatchesNotification()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        var dispatcher = new Mock<ITaskDispatcher>();
        var service = BuildService(db, dispatcher.Object);

        await service.CreateAsync(new MailCreateCommand(
            TraderId: 2002, Title: "T", Message: "M", SenderName: "S", SenderId: null,
            SymbolForReward: "", AmountForReward: 0));

        dispatcher.Verify(d => d.SendTaskAsync("notification", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NotificationOff_DoesNotDispatch()
    {
        using var db = CreateDb();
        var trader = ArkWallet.Domain.Entities.Trader.Create(2002, "u");
        trader.NotificationOn = false;
        db.Traders.Add(trader);
        await db.SaveChangesAsync();

        var dispatcher = new Mock<ITaskDispatcher>();
        var service = BuildService(db, dispatcher.Object);

        await service.CreateAsync(new MailCreateCommand(
            TraderId: 2002, Title: "T", Message: "M", SenderName: "S", SenderId: null,
            SymbolForReward: "", AmountForReward: 0));

        dispatcher.Verify(d => d.SendTaskAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task CreateManyAsync_EmptyList_ReturnsEmptyOk()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.CreateManyAsync(new List<MailCreateCommand>());

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var ids));
        Assert.Empty(ids);
        Assert.False(await db.MailMessages.AnyAsync());
    }

    [Fact]
    public async Task CreateManyAsync_CreatesMultipleMails()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.CreateManyAsync(new List<MailCreateCommand>
        {
            new(2002, "T1", "M1", "S", null, "", 0),
            new(3003, "T2", "M2", "S", null, "", 0)
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var ids));
        Assert.Equal(2, ids.Count);
        Assert.Equal(2, await db.MailMessages.CountAsync());
    }
}
