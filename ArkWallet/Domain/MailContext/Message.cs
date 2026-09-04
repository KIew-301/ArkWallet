using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Domain.MailContext;

/// <summary>
/// Message aggregate in the Mail context. Holds the mail status transitions and reward receipt rules.
/// </summary>
internal class Message : AggregateRoot
{

    public long Id { get; }
    public long TraderId { get; }
    public string Title { get; }
    public string Body { get; }
    public string SenderName { get; }
    public long? SenderId { get; }
    public string SymbolForReward { get; }
    public decimal AmountForReward { get; }
    public MailType Type { get; }
    public MailMessageStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }

    /// <summary>
    /// Whether this message carries a collectable reward.
    /// </summary>
    public bool HasReward => !string.IsNullOrWhiteSpace(SymbolForReward) && AmountForReward > 0;

    private Message(
        long id,
        long traderId,
        string title,
        string body,
        string senderName,
        long? senderId,
        string symbolForReward,
        decimal amountForReward,
        MailType type,
        MailMessageStatus status,
        DateTime createdAt,
        DateTime? readAt,
        DateTime? acceptedAt)
    {
        Id = id;
        TraderId = traderId;
        Title = title;
        Body = body;
        SenderName = senderName;
        SenderId = senderId;
        SymbolForReward = symbolForReward;
        AmountForReward = amountForReward;
        Type = type;
        Status = status;
        CreatedAt = createdAt;
        ReadAt = readAt;
        AcceptedAt = acceptedAt;
    }

    /// <summary>
    /// Rehydrates a Message from its persistence record.
    /// </summary>
    internal static Message Load(
        long id,
        long traderId,
        string title,
        string body,
        string senderName,
        long? senderId,
        string symbolForReward,
        decimal amountForReward,
        MailType type,
        MailMessageStatus status,
        DateTime createdAt,
        DateTime? readAt,
        DateTime? acceptedAt)
    {
        return new Message(
            id, traderId, title, body, senderName, senderId,
            symbolForReward, amountForReward, type, status,
            createdAt, readAt, acceptedAt);
    }

    /// <summary>
    /// Creates a new message of the given type (reward/notification) for a recipient.
    /// </summary>
    public static Message Create(
        long traderId,
        string title,
        string body,
        string senderName,
        long? senderId,
        string symbolForReward,
        decimal amountForReward,
        MailType type,
        DateTime createdAt)
    {
        return new Message(
            id: 0,
            traderId: traderId,
            title: title,
            body: body,
            senderName: senderName,
            senderId: senderId,
            symbolForReward: symbolForReward,
            amountForReward: amountForReward,
            type: type,
            status: MailMessageStatus.Sent,
            createdAt: createdAt,
            readAt: null,
            acceptedAt: null);
    }

    /// <summary>
    /// Marks the message as read. No-op when already read or accepted.
    /// </summary>
    public void MarkAsRead(DateTime readAt)
    {
        if (Status != MailMessageStatus.Sent)
            return;

        Status = MailMessageStatus.Read;
        ReadAt = readAt;
    }

    /// <summary>
    /// Marks the message reward as accepted. Validates that a reward exists and is not yet collected,
    /// then raises the reward-accepted domain event for the Portfolio context to credit the tokens.
    /// </summary>
    public async Task MarkAsAccepted(DateTime acceptedAt)
    {
        if (Status == MailMessageStatus.Accepted)
            throw new DomainException("Награда уже принята");

        if (!HasReward)
            throw new DomainException("Письмо не содержит награды");

        Status = MailMessageStatus.Accepted;
        AcceptedAt = acceptedAt;

        await PublishAsync(new MailRewardAcceptedEvent(
            TraderId,
            SymbolForReward,
            AmountForReward));
    }
}
