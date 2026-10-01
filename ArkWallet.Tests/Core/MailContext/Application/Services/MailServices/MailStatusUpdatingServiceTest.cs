using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MailContext.Application.Services.MailServices;
using ArkWallet.Core.General.Domain.Common;
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
using ArkWallet.Core.MailContext.Domain.Events;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Core.MailContext.Application.Services.MailServices;

public class MailStatusUpdatingServiceTest
{
    private static ArkWalletDbContext CreateDb() =>
        DbTest.CreateInitializedDbContextAsync().GetAwaiter().GetResult();

    private static MailMessage SeedMail(
        ArkWalletDbContext db,
        long traderId,
        string symbolForReward,
        decimal amount,
        MailType type)
    {
        var mail = Mapper.ToRecord(Message.Create(new MessageDraft(
            traderId, "Title", "Body", "System", null,
            symbolForReward, amount, type, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc))));
        db.MailMessages.Add(mail);
        db.SaveChanges();
        return mail;
    }

    private static StatusUpdatingService BuildService(
        ArkWalletDbContext db,
        IEventPublisher? publisher = null)
        => new StatusUpdatingService(
            db,
            publisher ?? new Mock<IEventPublisher>().Object,
            NullLogger<StatusUpdatingService>.Instance,
            new TestTimeProvider());

    [Fact]
    public async Task MarkAsReadAsync_ExistingMail_MarksRead()
    {
        using var db = CreateDb();
        var mail = SeedMail(db, 2002, "", 0, MailType.Notification);

        var service = BuildService(db);
        var result = await service.MarkAsReadAsync(mail.Id, 2002);

        Assert.True(result.IsSuccess);
        var updated = db.MailMessages.Single();
        Assert.Equal(MailMessageStatus.Read.ToString(), updated.Status);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkAsReadAsync_MailNotFound_ReturnsFail()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.MarkAsReadAsync(999, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Письмо не найдено", result.Message);
    }

    [Fact]
    public async Task MarkAsReadAsync_OtherTradersMail_ReturnsNotFound()
    {
        using var db = CreateDb();
        var mail = SeedMail(db, 2002, "", 0, MailType.Notification);

        var service = BuildService(db);
        var result = await service.MarkAsReadAsync(mail.Id, 3003);

        Assert.False(result.IsSuccess);
        Assert.Equal("Письмо не найдено", result.Message);
    }

    [Fact]
    public async Task MarkAsAcceptedAsync_WithReward_AcceptedAndPublishesEvent()
    {
        using var db = CreateDb();
        var mail = SeedMail(db, 2002, "ZZZ", 5, MailType.Reward);

        var publisher = new Mock<IEventPublisher>();
        var service = BuildService(db, publisher.Object);
        var result = await service.MarkAsAcceptedAsync(mail.Id, 2002);

        Assert.True(result.IsSuccess);
        var updated = db.MailMessages.Single();
        Assert.Equal(MailMessageStatus.Accepted.ToString(), updated.Status);
        Assert.NotNull(updated.AcceptedAt);
        publisher.Verify(p => p.PublishAsync(
            It.Is<MailRewardAcceptedEvent>(e => e.TraderId == 2002 && e.Symbol == "ZZZ" && e.Amount == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsAcceptedAsync_NoReward_ReturnsFail()
    {
        using var db = CreateDb();
        var mail = SeedMail(db, 2002, "", 0, MailType.Notification);

        var service = BuildService(db);
        var result = await service.MarkAsAcceptedAsync(mail.Id, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Письмо не содержит награды", result.Message);
    }

    [Fact]
    public async Task MarkAsAcceptedAsync_AlreadyAccepted_ReturnsFail()
    {
        using var db = CreateDb();
        var mail = SeedMail(db, 2002, "ZZZ", 5, MailType.Reward);

        var service = BuildService(db);
        var first = await service.MarkAsAcceptedAsync(mail.Id, 2002);
        Assert.True(first.IsSuccess);

        var second = await service.MarkAsAcceptedAsync(mail.Id, 2002);

        Assert.False(second.IsSuccess);
        Assert.Equal("Награда уже принята", second.Message);
    }

    [Fact]
    public async Task MarkAsAcceptedAsync_MailNotFound_ReturnsFail()
    {
        using var db = CreateDb();

        var service = BuildService(db);
        var result = await service.MarkAsAcceptedAsync(999, 2002);

        Assert.False(result.IsSuccess);
        Assert.Equal("Письмо не найдено", result.Message);
    }

    [Fact]
    public async Task MarkAllRewardMailsAsAcceptedAsync_AcceptsOnlyMailsWithAvailableReward()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 2002);

        MailMessage m1 = SeedMail(db, 2002, "ZZZ", 5, MailType.Reward);
        m1.Status = "Sent";
        db.SaveChanges();

        MailMessage m2 = SeedMail(db, 2002, "XXX", 3, MailType.Reward);
        m2.Status = "Read";
        m2.ReadAt = new DateTime(2026, 1, 1, 13, 0, 0);
        db.SaveChanges();

        MailMessage m3 = SeedMail(db, 2002, "", 0, MailType.Notification);
        m3.Status = "Sent";
        db.SaveChanges();

        MailMessage m4 = SeedMail(db, 2002, "YYY", 7, MailType.Reward);
        m4.Status = "Accepted";
        m4.AcceptedAt = new DateTime(2026, 1, 1, 14, 0, 0);
        db.SaveChanges();

        var service = BuildService(db);

        var result1 = await service.MarkAllRewardMailsAsAcceptedAsync(2002);

        Assert.True(result1.IsSuccess);
        Assert.True(result1.TryGetData(out var count1));
        Assert.Equal(2, count1);

        foreach (var mm in db.MailMessages)
        {
            if (mm.Title == "Title")
            {
                switch (mm.Id)
                {
                    case var _ when mm.Id == m1.Id:
                        Assert.Equal("Accepted", mm.Status);
                        break;
                    case var _ when mm.Id == m2.Id:
                        Assert.Equal("Accepted", mm.Status);
                        break;
                    case var _ when mm.Id == m3.Id:
                        Assert.Equal("Sent", mm.Status);
                        break;
                    case var _ when mm.Id == m4.Id:
                        Assert.Equal("Accepted", mm.Status);
                        break;
                }
            }
        }

        var result2 = await service.MarkAllRewardMailsAsAcceptedAsync(2002);

        Assert.True(result2.IsSuccess);
        Assert.True(result2.TryGetData(out var count2));
        Assert.Equal(0, count2);

        Assert.Equal(4, db.MailMessages.Count());
    }

}
