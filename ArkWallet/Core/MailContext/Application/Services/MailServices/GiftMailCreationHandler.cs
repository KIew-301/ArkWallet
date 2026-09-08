using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.GiftContext.Domain.Events;
using MediatR;

namespace ArkWallet.Core.MailContext.Application.Services.MailServices;

/// <summary>
/// Перехватывает отправку подарка и через MailMessageService создаёт Gift-письмо получателю.
/// </summary>
internal sealed class GiftMailCreationHandler(
    IMailMessageService mailMessageService) : INotificationHandler<GiftSentEvent>
{
    public async Task Handle(GiftSentEvent notification, CancellationToken cancellationToken)
    {
        var command = new MailCreateCommand(
            notification.RecipientId,
            "🎁 Вам отправили подарок!",
            $"Вам отправили {notification.Quantity} {notification.Symbol} от участника {notification.SenderName}.",
            notification.SenderName,
            notification.SenderId,
            notification.Symbol,
            notification.Quantity,
            Type: MailType.Gift.ToString());

        await mailMessageService.CreateAsync(command);
    }
}
