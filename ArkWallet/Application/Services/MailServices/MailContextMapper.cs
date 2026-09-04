using ArkWallet.Domain.Entities;
using ArkWallet.Domain.MailContext;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.MailServices;

/// <summary>
/// Maps between the MailMessage persistence records and the Message aggregate.
/// </summary>
internal static class MailContextMapper
{
    internal static Message ToMessage(Records.MailMessage record)
    {
        return Message.Load(
            record.Id,
            record.TraderId,
            record.Title,
            record.Message,
            record.SenderName,
            record.SenderId,
            record.SymbolForReward,
            record.AmountForReward,
            ParseEnum(record.Type, MailType.Notification),
            ParseEnum(record.Status, MailMessageStatus.Sent),
            record.CreatedAt,
            record.ReadAt,
            record.AcceptedAt);
    }

    internal static void ApplyToRecord(Records.MailMessage record, Message message)
    {
        record.TraderId = message.TraderId;
        record.Title = message.Title;
        record.Message = message.Body;
        record.SenderName = message.SenderName;
        record.SenderId = message.SenderId;
        record.SymbolForReward = message.SymbolForReward;
        record.AmountForReward = message.AmountForReward;
        record.Type = message.Type.ToString();
        record.Status = message.Status.ToString();
        record.CreatedAt = message.CreatedAt;
        record.ReadAt = message.ReadAt;
        record.AcceptedAt = message.AcceptedAt;
    }

    internal static Records.MailMessage ToRecord(Message message)
    {
        return new Records.MailMessage
        {
            TraderId = message.TraderId,
            Title = message.Title,
            Message = message.Body,
            SenderName = message.SenderName,
            SenderId = message.SenderId,
            SymbolForReward = message.SymbolForReward,
            AmountForReward = message.AmountForReward,
            Type = message.Type.ToString(),
            Status = message.Status.ToString(),
            CreatedAt = message.CreatedAt,
            ReadAt = message.ReadAt,
            AcceptedAt = message.AcceptedAt
        };
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum
        => Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;
}
