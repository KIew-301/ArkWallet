using MediatR;

namespace ArkWallet.Domain.GiftContext;

internal sealed record GiftSentEvent(
    Guid GiftId,
    long SenderId,
    long RecipientId,
    string TokenSymbol,
    decimal Quantity,
    DateTime SentAt
) : INotification;

internal sealed record GiftReceivedEvent(
    Guid GiftId,
    long SenderId,
    long RecipientId,
    string TokenSymbol,
    decimal Quantity,
    DateTime ReceivedAt
) : INotification;

internal sealed record AllGiftsReceivedEvent(
    long RecipientId,
    IReadOnlyList<GiftReceivedData> Gifts,
    DateTime ReceivedAt
) : INotification;

internal sealed record GiftSendRejectedEvent(
    long SenderId,
    long RecipientId,
    string TokenSymbol,
    decimal Quantity,
    GiftRejectReason Reason,
    DateTime RejectedAt
) : INotification;

internal sealed record GiftReceivedData(
    Guid GiftId,
    long SenderId,
    string TokenSymbol,
    decimal Quantity
);

internal enum GiftRejectReason
{
    LimitExceeded,
    PriceTooHigh,
    InsufficientBalance,
    RecipientNotRegistered,
    SelfGift
}
