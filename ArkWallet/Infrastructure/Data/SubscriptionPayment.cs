using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArkWallet.Infrastructure.Data
{
    internal class SubscriptionPayment : EntityData
    {
        [Key]
        public int Id { get; set; }
        public long TraderId { get; set; }
        public int SubscriptionId { get; set; }
        public int Period { get; set; }
        public decimal AmountRubles { get; set; }
        public string ExternalPaymentId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ConfirmationUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? SucceededAtUtc { get; set; }
        public DateTime? CanceledAtUtc { get; set; }
    }
}
