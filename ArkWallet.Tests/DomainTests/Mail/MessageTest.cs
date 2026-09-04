using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.MailContext;
using Moq;

namespace ArkWallet.Tests.DomainTests.Mail;

public class MessageTest
{
    private static readonly DateTime Now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Message CreateRewardMessage(IEventPublisher? publisher = null)
    {
        var message = Message.Create(
            traderId: 2002,
            title: "Title",
            body: "Body",
            senderName: "System",
            senderId: null,
            symbolForReward: "ZZZ",
            amountForReward: 5,
            type: MailType.Reward,
            createdAt: Now);

        message.SetEventPublisher(publisher ?? new Mock<IEventPublisher>().Object);
        return message;
    }

    private static Message CreatePlainMessage()
        => Message.Create(2002, "Title", "Body", "System", null, "", 0, MailType.Notification, Now);

    [Fact]
    public void Create_SetsFieldsAndSentStatus()
    {
        var message = CreatePlainMessage();

        Assert.Equal(2002, message.TraderId);
        Assert.Equal("Title", message.Title);
        Assert.Equal("Body", message.Body);
        Assert.Equal("System", message.SenderName);
        Assert.Null(message.SenderId);
        Assert.Equal(MailType.Notification, message.Type);
        Assert.Equal(MailMessageStatus.Sent, message.Status);
        Assert.Equal(Now, message.CreatedAt);
        Assert.Null(message.ReadAt);
        Assert.Null(message.AcceptedAt);
    }

    [Fact]
    public void HasReward_WithSymbolAndPositiveAmount_ReturnsTrue()
    {
        Assert.True(CreateRewardMessage().HasReward);
    }

    [Fact]
    public void HasReward_EmptySymbol_ReturnsFalse()
    {
        Assert.False(CreatePlainMessage().HasReward);
    }

    [Fact]
    public void MarkAsRead_FromSent_SetsRead()
    {
        var message = CreatePlainMessage();

        message.MarkAsRead(Now);

        Assert.Equal(MailMessageStatus.Read, message.Status);
        Assert.Equal(Now, message.ReadAt);
    }

    [Fact]
    public void MarkAsRead_AlreadyRead_IsNoOp()
    {
        var message = CreatePlainMessage();
        message.MarkAsRead(Now);
        message.MarkAsRead(Now.AddHours(1));

        Assert.Equal(MailMessageStatus.Read, message.Status);
        Assert.Equal(Now, message.ReadAt);
    }

    [Fact]
    public async Task MarkAsRead_AlreadyAccepted_IsNoOp()
    {
        var message = CreateRewardMessage();
        await message.MarkAsAccepted(Now);
        Assert.Equal(Now, message.AcceptedAt);

        message.MarkAsRead(Now.AddHours(2));

        Assert.Equal(MailMessageStatus.Accepted, message.Status);
        Assert.Null(message.ReadAt);
    }

    [Fact]
    public async Task MarkAsAccepted_WithReward_AcceptedAndPublishesEvent()
    {
        var publisher = new Mock<IEventPublisher>();
        var message = CreateRewardMessage(publisher.Object);

        await message.MarkAsAccepted(Now);

        Assert.Equal(MailMessageStatus.Accepted, message.Status);
        Assert.Equal(Now, message.AcceptedAt);
        publisher.Verify(p => p.PublishAsync(
            It.Is<MailRewardAcceptedEvent>(e =>
                e.TraderId == 2002 && e.Symbol == "ZZZ" && e.Amount == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsAccepted_AlreadyAccepted_Throws()
    {
        var message = CreateRewardMessage();
        await message.MarkAsAccepted(Now);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => message.MarkAsAccepted(Now.AddHours(1)));

        Assert.Equal("Награда уже принята", ex.Message);
    }

    [Fact]
    public async Task MarkAsAccepted_NoReward_Throws()
    {
        var message = CreatePlainMessage();

        var ex = await Assert.ThrowsAsync<DomainException>(() => message.MarkAsAccepted(Now));

        Assert.Equal("Письмо не содержит награды", ex.Message);
    }

    [Fact]
    public async Task MarkAsAccepted_FromReadState_IsAllowed()
    {
        var publisher = new Mock<IEventPublisher>();
        var message = CreateRewardMessage(publisher.Object);
        message.MarkAsRead(Now);

        await message.MarkAsAccepted(Now.AddMinutes(5));

        Assert.Equal(MailMessageStatus.Accepted, message.Status);
        Assert.Equal(Now.AddMinutes(5), message.AcceptedAt);
        publisher.Verify(p => p.PublishAsync(It.IsAny<MailRewardAcceptedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
