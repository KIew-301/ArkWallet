using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArkWallet.Infrastructure.Data
{
    internal class Subscription : EntityData
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public decimal PriceRubles { get; set; }
        public decimal PriceWeekRubles { get; set; }
        public decimal PriceMonthRubles { get; set; }
        public decimal PriceYearRubles { get; set; }
        public int MaxOrders { get; set; }
        public int MaxMiningMachines { get; set; }
        public int? DurationMinutes { get; set; }
        public virtual ICollection<SubscriptionPurchaseHistory> PurchaseHistory { get; set; } = new List<SubscriptionPurchaseHistory>();
    }
}
