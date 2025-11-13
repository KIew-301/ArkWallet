using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArkWallet.Domain.Entities
{
    internal class Trader
    {
        [Key]
        public long TelegramId { get; set; }
        public string? Username { get; set; }
        public decimal Balance { get; set; } = 1000.0m;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<PortfolioItem> Portfolio { get; set; } = new List<PortfolioItem>();
        public virtual ICollection<TradeOrder> Orders { get; set; } = new List<TradeOrder>();
        public bool CanAfford(decimal amount)
            => Balance >= amount;

        // Необходимость обновления в БД
        [NotMapped]
        public bool IsDirty { get; private set; }

        public void MarkDirty() => IsDirty = true;
        public void MarkClean() => IsDirty = false;

        public static Trader Create(long telegramId, string? username)
        {
            return new Trader
            {
                TelegramId = telegramId,
                Username = username,
                Balance = 1000.0m,
                JoinedAt = DateTime.UtcNow
            };
        }
    }
}
