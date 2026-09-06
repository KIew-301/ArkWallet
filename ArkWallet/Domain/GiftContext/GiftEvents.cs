using MediatR;

namespace ArkWallet.Domain.GiftContext;

/// <summary>
/// Raised when a gift is sent. Reacted to by the Mail context (creates the gift message).
/// Token removal from the sender's portfolio is handled by the User aggregate itself.
/// </summary>
internal sealed record GiftSentEvent(
    long SenderId,
    long RecipientId,
    string SenderName,
    string Symbol,
    int Quantity,
    DateTime CreatedAt) : INotification;
