using ArkWallet.Core.General.Domain.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data
{
    internal class PortfolioItem : EntityData
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Внешние ключи
        public long TraderTelegramId { get; private set; }
        public string CharacterTokenId { get; private set; }

        // Данные владения
        public int Quantity { get; set; }
        public int SellingQuantity { get; set; }
        public int ReserveQuantity { get; set; }
        public decimal AverageBuyPrice { get; set; }
        public decimal AverageSellPrice { get; set; }
        public decimal AverageReservePrice { get; set; }
        public DateTime AcquiredAt { get; private set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual Trader? Trader { get; set; }
        public virtual CharacterToken? CharacterToken { get; set; }

        // Методы
        public decimal GetTotalValue
            => Quantity * AverageBuyPrice;

        /// <summary>
        /// Переносит полное состояние портфеля (используется при сохранении результатов торгового движка).
        /// </summary>
        public void Update(
            int quantity,
            int sellingQuantity,
            int reserveQuantity,
            decimal averageBuyPrice,
            decimal averageSellPrice,
            decimal averageReservePrice)
        {
            Quantity = quantity;
            SellingQuantity = sellingQuantity;
            ReserveQuantity = reserveQuantity;
            AverageBuyPrice = averageBuyPrice;
            AverageSellPrice = averageSellPrice;
            AverageReservePrice = averageReservePrice;
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
