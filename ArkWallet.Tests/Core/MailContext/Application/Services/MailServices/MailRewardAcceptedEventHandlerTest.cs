using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.MailContext.Application.Services.MailServices;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MailContext.Domain.Events;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using Moq;

namespace ArkWallet.Tests.Core.MailContext.Application.Services.MailServices;

public class MailRewardAcceptedEventHandlerTest
{
    [Fact]
    public async Task Handle_ChangePositionAddsRewardTokens()
    {
        var portfolioService = new Mock<IPortfolioUpdatingService>();
        var handler = new MailRewardAcceptedEventHandler(portfolioService.Object);

        await handler.Handle(
            new MailRewardAcceptedEvent(2002, "ZZZ", 5),
            CancellationToken.None);

        portfolioService.Verify(
            s => s.ChangePositionAsync(
                It.Is<PortfolioChangeCommand>(c =>
                    c.TraderId == 2002 &&
                    c.Symbol == "ZZZ" &&
                    c.Type == PortfolioChangeType.Add &&
                    c.Quantity == 5)),
            Times.Once);
    }
}
