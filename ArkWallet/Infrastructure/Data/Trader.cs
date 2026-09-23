using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data
{
    internal class Trader : EntityData
    {
        [Key]
        public long TelegramId { get; set; }
        public string? Username { get; set; }
        public decimal Balance { get; set; } = DefaultBalance;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool NotificationOn { get; set; }
        public int? SubscriptionId { get; set; }
        public virtual Subscription? Subscription { get; set; }
        public DateTime? SubscriptionExpiresAtUtc { get; set; }
        public string? SavedPaymentMethodId { get; set; }
        public virtual ICollection<PortfolioItem> Portfolio { get; set; } = new List<PortfolioItem>();
        public virtual ICollection<TradeOrder> Orders { get; set; } = new List<TradeOrder>();
        public virtual ICollection<BalanceSnapshot> BalanceSnapshots { get; set; } = new List<BalanceSnapshot>();
        public virtual ICollection<SubscriptionPurchaseHistory> SubscriptionPurchaseHistory { get; set; } = new List<SubscriptionPurchaseHistory>();
        public static Trader Create(long telegramId, string? username)
        {
            return new Trader
            {
                TelegramId = telegramId,
                Username = username,
                Balance = DefaultBalance,
                JoinedAt = DateTime.UtcNow,
                NotificationOn = true
            };
        }

        public const decimal DefaultBalance = 1000.0m;
    }
}
