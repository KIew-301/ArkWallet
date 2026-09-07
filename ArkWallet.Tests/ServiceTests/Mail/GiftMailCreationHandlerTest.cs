using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Application.Services.MailServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.GiftContext;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Mail;

public class GiftMailCreationHandlerTest
{
    [Fact]
    public async Task Handle_SendsGiftMailCommandToMailMessageService()
    {
        var mailService = new Mock<IMailMessageService>();
        var handler = new GiftMailCreationHandler(mailService.Object);

        await handler.Handle(new GiftSentEvent(
            SenderId: 1001,
            RecipientId: 2002,
            SenderName: "Sender",
            Symbol: "ZZZ",
            Quantity: 1,
            CreatedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        mailService.Verify(m => m.CreateAsync(It.Is<MailCreateCommand>(c =>
            c.TraderId == 2002 &&
            c.SenderId == 1001 &&
            c.SenderName == "Sender" &&
            c.SymbolForReward == "ZZZ" &&
            c.AmountForReward == 1 &&
            c.Type == MailType.Gift.ToString() &&
            c.Title.Contains("подарок", StringComparison.OrdinalIgnoreCase) &&
            c.Message.Contains("ZZZ"))), Times.Once);
    }
}
