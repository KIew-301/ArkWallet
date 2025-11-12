using ArkWallet.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Entities
{
    internal class TradeOrder
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Основные данные
        public OrderType Type { get; set; }              // Buy/Sell
        public OrderStatus Status { get; set; } = OrderStatus.Active;
        public string CharacterTokenId { get; set; }     // Какой токен
        public long TraderTelegramId { get; set; }       // Кто разместил
        public decimal Price { get; set; }               // По какой цене
        public int Quantity { get; set; }                // Сколько хочет
        public int FilledQuantity { get; set; } = 0;     // Сколько уже исполнилось

        // Временные метки
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExecutedAt { get; set; }

        // Навигационные свойства
        public virtual Trader? Trader { get; set; }
        public virtual CharacterToken? CharacterToken { get; set; }

        // Методы
        public bool IsFilled() => FilledQuantity >= Quantity;
        public bool IsActive() => Status == OrderStatus.Active;
        public int GetRemainingQuantity() => Quantity - FilledQuantity;

        public void MarkAsFilled()
        {
            Status = OrderStatus.Filled;
            ExecutedAt = DateTime.UtcNow;
            FilledQuantity = Quantity;
        }

        public void Cancel()
        {
            if (IsActive())
            {
                Status = OrderStatus.Cancelled;
            }
        }

        public TradeOrder WithQuantity(int newQuantity)
        {
            return new TradeOrder
            {
                Id = Guid.NewGuid().ToString(), // Новый Id
                Quantity = newQuantity,
                FilledQuantity = 0, // Сбрасываем исполненное количество
                Price = this.Price,
                Type = this.Type,
                TraderTelegramId = this.TraderTelegramId,
                CharacterTokenId = this.CharacterTokenId,
                Status = OrderStatus.Active, // Новый статус
                CreatedAt = DateTime.UtcNow // Новое время создания
            };
        }
    }
}
