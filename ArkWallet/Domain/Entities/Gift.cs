using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities;

internal class Gift
{
    [Key]
    public Guid Id { get; set; }
    public long SenderId { get; set; }
    public long RecipientId { get; set; }
    public string TokenSymbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PriceAtSend { get; set; }
    public string Status { get; set; } = "Sent";
    public DateTime SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    public static Gift Create(
        Guid id,
        long senderId,
        long recipientId,
        string tokenSymbol,
        decimal quantity,
        decimal priceAtSend,
        DateTime sentAt)
    {
        return new Gift
        {
            Id = id,
            SenderId = senderId,
            RecipientId = recipientId,
            TokenSymbol = tokenSymbol,
            Quantity = quantity,
            PriceAtSend = priceAtSend,
            Status = "Sent",
            SentAt = sentAt
        };
    }

    public void MarkAsReceived(DateTime receivedAt)
    {
        Status = "Received";
        ReceivedAt = receivedAt;
    }
}
