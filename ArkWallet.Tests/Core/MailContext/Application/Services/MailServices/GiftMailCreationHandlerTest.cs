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
using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.GiftContext.Domain.Events;
using ArkWallet.Infrastructure.Data;
using Moq;

namespace ArkWallet.Tests.Core.MailContext.Application.Services.MailServices;

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
