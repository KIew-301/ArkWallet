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

    private Message(MessageData data)
    {
        Id = data.Id;
        TraderId = data.TraderId;
        Title = data.Title;
        Body = data.Body;
        SenderName = data.SenderName;
        SenderId = data.SenderId;
        SymbolForReward = data.SymbolForReward;
        AmountForReward = data.AmountForReward;
        Type = data.Type;
        Status = data.Status;
        CreatedAt = data.CreatedAt;
        ReadAt = data.ReadAt;
        AcceptedAt = data.AcceptedAt;
    }

    /// <summary>
    /// Rehydrates a Message from its persistence record.
    /// </summary>
    internal static Message Load(MessageData data)
    {
        return new Message(data);
    }

    /// <summary>
    /// Creates a new message of the given type (reward/notification) for a recipient.
    /// </summary>
    public static Message Create(MessageDraft draft)
    {
        return new Message(new MessageData(
            Id: 0,
            TraderId: draft.TraderId,
            Title: draft.Title,
            Body: draft.Body,
            SenderName: draft.SenderName,
            SenderId: draft.SenderId,
            SymbolForReward: draft.SymbolForReward,
            AmountForReward: draft.AmountForReward,
            Type: draft.Type,
            Status: MailMessageStatus.Sent,
            CreatedAt: draft.CreatedAt,
            ReadAt: null,
            AcceptedAt: null));
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

/// <summary>
/// Flat persistence snapshot of a <see cref="Message"/>.
/// </summary>
internal sealed record MessageData(
    long Id,
    long TraderId,
    string Title,
    string Body,
    string SenderName,
    long? SenderId,
    string SymbolForReward,
    decimal AmountForReward,
    MailType Type,
    MailMessageStatus Status,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? AcceptedAt);

/// <summary>
/// Draft of a new message to be created.
/// </summary>
internal sealed record MessageDraft(
    long TraderId,
    string Title,
    string Body,
    string SenderName,
    long? SenderId,
    string SymbolForReward,
    decimal AmountForReward,
    MailType Type,
    DateTime CreatedAt);
