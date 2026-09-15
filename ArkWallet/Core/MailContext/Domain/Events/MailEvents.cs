using MediatR;

namespace ArkWallet.Core.MailContext.Domain.Events;

/// <summary>
/// Raised when a mail reward is accepted. The Portfolio context reacts by adding the tokens to the trader.
/// </summary>
internal sealed record MailRewardAcceptedEvent(
    long TraderId,
    string Symbol,
    decimal Amount) : INotification;
