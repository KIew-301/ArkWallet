using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.MailContext;
using ArkWallet.Domain.PortfolioContext;
using MediatR;

namespace ArkWallet.Application.Services.MailServices;

/// <summary>
/// Reacts to a mail reward being accepted by adding the tokens to the trader's portfolio
/// without changing the average buy price.
/// </summary>
internal sealed class MailRewardAcceptedEventHandler(
    IPortfolioUpdatingService portfolioUpdatingService) : INotificationHandler<MailRewardAcceptedEvent>
{
    public async Task Handle(MailRewardAcceptedEvent notification, CancellationToken cancellationToken)
    {
        await portfolioUpdatingService.ChangePositionAsync(new PortfolioChangeCommand(
            notification.TraderId,
            notification.Symbol,
            PortfolioChangeType.Add,
            (int)notification.Amount,
            0));
    }
}
