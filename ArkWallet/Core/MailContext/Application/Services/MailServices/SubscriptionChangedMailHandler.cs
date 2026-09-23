using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using MediatR;

namespace ArkWallet.Core.MailContext.Application.Services.MailServices;

/// <summary>
/// Перехватывает события подписки (покупка, продление, улучшение, истечение)
/// и через MessageService создаёт уведомление трейдеру в почтовый ящик.
/// </summary>
internal sealed class SubscriptionChangedMailHandler(
    IMessageService MessageService) : INotificationHandler<TraderSubscriptionChangedEvent>
{
    public async Task Handle(TraderSubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        var subscriptionName = notification.SubscriptionName ?? "Подписка";
        var expiresText = notification.ExpiresAtUtc.HasValue
            ? $" До: {notification.ExpiresAtUtc.Value:yyyy-MM-dd HH:mm} UTC."
            : " Без срока действия.";

        var (title, message) = notification.Operation switch
        {
            SubscriptionChangeOperation.Purchased => (
                "✅ Подписка активирована",
                $"Вы приобрели подписку «{subscriptionName}» (уровень {notification.SubscriptionLevel}).{expiresText}"),
            SubscriptionChangeOperation.Renewed => (
                "🔄 Подписка продлена",
                $"Подписка «{subscriptionName}» (уровень {notification.SubscriptionLevel}) продлена.{expiresText}"),
            SubscriptionChangeOperation.Upgraded => (
                "⬆️ Подписка улучшена",
                $"Вы перешли на подписку «{subscriptionName}» (уровень {notification.SubscriptionLevel}).{expiresText}"),
            SubscriptionChangeOperation.Expired => (
                "⏳ Срок подписки истёк",
                $"Срок подписки «{subscriptionName}» (уровень {notification.SubscriptionLevel}) истёк. Вы переведены на базовую подписку."),
            _ => (
                "📩 Уведомление о подписке",
                $"Ваша подписка «{subscriptionName}» (уровень {notification.SubscriptionLevel}) изменилась."),
        };

        var command = new CreateCommand(
            notification.TraderId,
            title,
            message,
            "Система",
            SenderId: null,
            string.Empty,
            0,
            Type: MailType.Notification.ToString());

        await MessageService.CreateAsync(command);
    }
}