using MediatR;

namespace ArkWallet.Core.SubscriptionContext.Application.Events;

internal enum SubscriptionChangeOperation
{
    Purchased,
    Renewed,
    Upgraded,
    Expired
}

internal sealed record TraderSubscriptionChangedEvent(
    long TraderId,
    SubscriptionChangeOperation Operation = SubscriptionChangeOperation.Purchased,
    string? SubscriptionName = null,
    int SubscriptionLevel = 0,
    DateTime? ExpiresAtUtc = null) : INotification;
