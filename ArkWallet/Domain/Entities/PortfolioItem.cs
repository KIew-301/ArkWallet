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
        public long TraderTelegramId { get; private set; }
        public string CharacterTokenId { get; private set; }

        // Данные владения
        public int Quantity { get; private set; }
        public int SellingQuantity { get; private set; }
        public int ReserveQuantity { get; private set; }
        public decimal AverageBuyPrice { get; private set; }
        public decimal AverageSellPrice { get; private set; }
        public decimal AverageReservePrice { get; private set; }
        public DateTime AcquiredAt { get; private set; } = DateTime.UtcNow;

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

        public void BuyTokens(int quantity, decimal buyPrice)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

            var totalCost = Quantity * AverageBuyPrice + quantity * buyPrice;
            Quantity += quantity;
            AverageBuyPrice = totalCost / Quantity;

            MarkDirty();
        }

        public void ReserveTokens(int quantity, decimal reservePrice)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

            Quantity -= quantity;

            var totalCost = ReserveQuantity * AverageReservePrice + quantity * reservePrice;
            ReserveQuantity += quantity;
            AverageReservePrice = totalCost / ReserveQuantity;

            if (Quantity == 0)
                AverageBuyPrice = 0;

            MarkDirty();
        }

        public void SellTokens(int quantity, decimal sellPrice)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

            ReserveQuantity -= quantity;

            var totalCost = SellingQuantity * AverageSellPrice + quantity * sellPrice;
            SellingQuantity += quantity;
            AverageSellPrice = totalCost / SellingQuantity;

            if (ReserveQuantity == 0)
                AverageReservePrice = 0;

            MarkDirty();
        }

        public void ReturnTokens(int quantity)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше 0");

            ReserveQuantity -= quantity;

            var totalCost = Quantity * AverageBuyPrice + quantity * AverageReservePrice;
            Quantity += quantity;
            AverageBuyPrice = totalCost / Quantity;

            if (ReserveQuantity == 0)
                AverageReservePrice = 0;

            MarkDirty();
        }

        public void RemoveTokens(int quantity, decimal buyPrice)
        {
            if (quantity <= 0) throw new DomainException("Количество токенов меньше или равно 0");
            if (quantity > Quantity) throw new DomainException("Больше токенов недостаточно");

            if (Quantity == 0)
                AverageBuyPrice = 0;

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
