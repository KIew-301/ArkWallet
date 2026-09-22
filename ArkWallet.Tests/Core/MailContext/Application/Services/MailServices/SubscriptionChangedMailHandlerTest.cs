using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.MailContext.Application.Services.MailServices;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using Moq;

namespace ArkWallet.Tests.Core.MailContext.Application.Services.MailServices;

public class SubscriptionChangedMailHandlerTest
{
    private readonly Mock<IMessageService> _mailService = new();
    private readonly SubscriptionChangedMailHandler _handler;

    public SubscriptionChangedMailHandlerTest()
    {
        _handler = new SubscriptionChangedMailHandler(_mailService.Object);
    }

    [Theory]
    [InlineData(0, "Подписка активирована")]
    [InlineData(1, "Подписка продлена")]
    [InlineData(2, "Подписка улучшена")]
    [InlineData(3, "Срок подписки истёк")]
    public async Task Handle_AnyOperation_SendsNotificationMailToTrader(int operationValue, string expectedTitle)
    {
        var operation = (SubscriptionChangeOperation)operationValue;
        await _handler.Handle(new TraderSubscriptionChangedEvent(
            TraderId: 2002,
            Operation: operation,
            SubscriptionName: "Премиум",
            SubscriptionLevel: 2,
            ExpiresAtUtc: new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        _mailService.Verify(m => m.CreateAsync(It.Is<CreateCommand>(c =>
            c.TraderId == 2002 &&
            c.SenderName == "Система" &&
            c.Type == MailType.Notification.ToString() &&
            c.Title.Contains(expectedTitle) &&
            c.Message.Contains("Премиум") &&
            c.Message.Contains("уровень 2"))), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpirationDate_IncludesExpiresText()
    {
        await _handler.Handle(new TraderSubscriptionChangedEvent(
            TraderId: 2002,
            Operation: SubscriptionChangeOperation.Purchased,
            SubscriptionName: "Премиум",
            SubscriptionLevel: 2,
            ExpiresAtUtc: new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        _mailService.Verify(m => m.CreateAsync(It.Is<CreateCommand>(c =>
            c.Message.Contains("2026-06-01 12:00") &&
            c.Message.Contains("UTC"))), Times.Once);
    }

    [Fact]
    public async Task Handle_NoExpirationDate_UsesNoDeadlineText()
    {
        await _handler.Handle(new TraderSubscriptionChangedEvent(
            TraderId: 2002,
            Operation: SubscriptionChangeOperation.Purchased,
            SubscriptionName: "Базовая",
            SubscriptionLevel: 1,
            ExpiresAtUtc: null), CancellationToken.None);

        _mailService.Verify(m => m.CreateAsync(It.Is<CreateCommand>(c =>
            c.Message.Contains("Без срока действия"))), Times.Once);
    }

    [Fact]
    public async Task Handle_NullSubscriptionName_FallsBackToDefault()
    {
        await _handler.Handle(new TraderSubscriptionChangedEvent(
            TraderId: 2002,
            Operation: SubscriptionChangeOperation.Expired,
            SubscriptionName: null,
            SubscriptionLevel: 0), CancellationToken.None);

        _mailService.Verify(m => m.CreateAsync(It.Is<CreateCommand>(c =>
            c.Message.Contains("Подписка") &&
            c.Title.Contains("истёк"))), Times.Once);
    }
}