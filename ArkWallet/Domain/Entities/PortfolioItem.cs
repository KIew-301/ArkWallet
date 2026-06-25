using ArkWallet.Domain.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArkWallet.Domain.Entities
{
    internal class PortfolioItem
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Внешние ключи
        public long TraderTelegramId { get; set; }
        public string CharacterTokenId { get; set; }

        // Данные владения
        public int Quantity { get; set; }
        public decimal AverageBuyPrice { get; set; }
        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual Trader? Trader { get; set; }
        public virtual CharacterToken? CharacterToken { get; set; }

        // Необходимость обновления в БД
        [NotMapped]
        public bool IsDirty { get; private set; }

        public void MarkDirty() => IsDirty = true;
        public void MarkClean() => IsDirty = false;

        // Методы
        public decimal GetTotalValue()
            => Quantity * AverageBuyPrice;

        public decimal GetCurrentValue(CharacterToken token)
            => Quantity * token.CurrentPrice;

        public decimal GetProfitLoss(CharacterToken token)
            => GetCurrentValue(token) - GetTotalValue();

        public void AddTokens(int quantity, decimal buyPrice)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

            // Пересчет средней цены
            var totalCost = Quantity * AverageBuyPrice + quantity * buyPrice;
            Quantity += quantity;
            AverageBuyPrice = totalCost / Quantity;

            MarkDirty();
        }

        public void ReturnTokens(int quantity)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");
            Quantity += quantity;
            MarkDirty();
        }

        public void RemoveTokens(int quantity)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше или равно 0");
            if (quantity > Quantity) throw new DomainException("Больше токенов недостаточно");

            Quantity -= quantity;
            MarkDirty();
        }

        public static PortfolioItem Create(long telegramId, string symbol, int quantity, decimal price)
        {
            if (quantity == 0) throw new DomainException("Количество токенов меньше 0");

            return new PortfolioItem
            {
                TraderTelegramId = telegramId,
                CharacterTokenId = symbol,
                Quantity = quantity,
                AverageBuyPrice = price
            };
        }
    }
}
