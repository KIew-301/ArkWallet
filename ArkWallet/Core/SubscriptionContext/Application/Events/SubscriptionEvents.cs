using MediatR;

namespace ArkWallet.Core.SubscriptionContext.Application.Events;

internal sealed record TraderSubscriptionChangedEvent(long TraderId) : INotification;
