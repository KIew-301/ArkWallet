using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.GiftContext.Domain.Events;
using MediatR;

namespace ArkWallet.Core.MailContext.Application.Services.MailServices;

/// <summary>
/// Перехватывает отправку подарка и через MessageService создаёт Gift-письмо получателю.
/// </summary>
internal sealed class GiftMailCreationHandler(
    IMessageService MessageService) : INotificationHandler<GiftSentEvent>
{
    public async Task Handle(GiftSentEvent notification, CancellationToken cancellationToken)
    {
        var command = new CreateCommand(
            notification.RecipientId,
            "🎁 Вам отправили подарок!",
            $"Вам отправили {notification.Quantity} {notification.Symbol} от участника {notification.SenderName}.",
            notification.SenderName,
            notification.SenderId,
            notification.Symbol,
            notification.Quantity,
            Type: MailType.Gift.ToString());

        await MessageService.CreateAsync(command);
    }
}
