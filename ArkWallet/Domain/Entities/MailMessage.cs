using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities;

/// <summary>
/// Письмо в почтовом ящике пользователя
/// </summary>
internal class MailMessage
{
    [Key]
    public long Id { get; set; }

    /// <summary>Telegram ID получателя</summary>
    public long TraderId { get; set; }

    /// <summary>Название письма</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Текст письма</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Имя отправителя</summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>ID отправителя (null, если системное письмо)</summary>
    public long? SenderId { get; set; }

    /// <summary>Символ токена награды (пусто, если награда не предполагается)</summary>
    public string SymbolForReward { get; set; } = string.Empty;

    /// <summary>Количество токенов награды (0, если награда не предполагается)</summary>
    public decimal AmountForReward { get; set; }

    /// <summary>Статус письма (Sent/Read/Accepted)</summary>
    public string Status { get; set; } = MailMessageStatus.Sent.ToString();

    /// <summary>Дата создания письма</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата прочтения письма (null, если не прочитано)</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Дата принятия награды из письма (null, если не принято)</summary>
    public DateTime? AcceptedAt { get; set; }

    public static MailMessage Create(
        long traderId,
        string title,
        string message,
        string senderName,
        long? senderId,
        string symbolForReward,
        decimal amountForReward,
        DateTime createdAt)
    {
        return new MailMessage
        {
            TraderId = traderId,
            Title = title,
            Message = message,
            SenderName = senderName,
            SenderId = senderId,
            SymbolForReward = symbolForReward,
            AmountForReward = amountForReward,
            Status = MailMessageStatus.Sent.ToString(),
            CreatedAt = createdAt
        };
    }

    public void MarkAsRead(DateTime readAt)
    {
        if (Status == MailMessageStatus.Sent.ToString())
        {
            Status = MailMessageStatus.Read.ToString();
            ReadAt = readAt;
        }
    }

    public void MarkAsAccepted(DateTime acceptedAt)
    {
        if (Status == MailMessageStatus.Sent.ToString() || Status == MailMessageStatus.Read.ToString())
        {
            Status = MailMessageStatus.Accepted.ToString();
            AcceptedAt = acceptedAt;
        }
    }
}

/// <summary>
/// Статус письма в почтовом ящике
/// </summary>
internal enum MailMessageStatus
{
    /// <summary>Отправлено — письмо создано и доставлено в ящик</summary>
    Sent,

    /// <summary>Прочитано — пользователь открыл письмо</summary>
    Read,

    /// <summary>Принято — пользователь принял награду из письма</summary>
    Accepted
}
