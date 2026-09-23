using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArkWallet.Infrastructure.Data
{
    internal class SubscriptionPurchaseHistory : EntityData
    {
        [Key]
        public long Id { get; set; }
        public long TraderId { get; set; }
        public int SubscriptionId { get; set; }
        public decimal PriceRubles { get; set; }
        public DateTime PurchasedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? TransactionId { get; set; }
        public virtual Trader Trader { get; set; } = null!;
        public virtual Subscription Subscription { get; set; } = null!;
    }
}
