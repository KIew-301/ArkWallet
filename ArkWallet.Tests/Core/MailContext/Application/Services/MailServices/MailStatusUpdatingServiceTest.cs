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
using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.MailContext.Domain.Message;
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
        var mail = MailContextMapper.ToRecord(Message.Create(new MessageDraft(
            traderId, "Title", "Body", "System", null,
            symbolForReward, amount, type, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc))));
        db.MailMessages.Add(mail);
        db.SaveChanges();
        return mail;
    }

    private static MailStatusUpdatingService BuildService(
        ArkWalletDbContext db,
        IEventPublisher? publisher = null)
        => new MailStatusUpdatingService(
            db,
            publisher ?? new Mock<IEventPublisher>().Object,
            NullLogger<MailStatusUpdatingService>.Instance,
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
}
