using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities
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
        public bool IsTraderOrder(long initiatorId) => TraderTelegramId == initiatorId;
        public int GetRemainingQuantity() => Quantity - FilledQuantity;

        public void MarkAsFilled()
        {
            Status = OrderStatus.Filled;
            ExecutedAt = DateTime.UtcNow;
            FilledQuantity = Quantity;
        }

        public static TradeOrder Create(OrderType orderType, string symbol,
            long traderId, decimal price, int quantity)
        {
            if (price <= 0)
                throw new DomainException("Цена должна быть больше 0");

            if (quantity <= 0)
                throw new DomainException("Количество токенов должно быть больше 0");

            return new()
            {
                Type = orderType,
                CharacterTokenId = symbol,
                TraderTelegramId = traderId,
                Price = price,
                Quantity = quantity
            };
        }

        public void Cancel(long initiatorId)
        {
            if (!IsTraderOrder(initiatorId))
                throw new DomainException("Нельзя отменить чужой ордер.");

            if (!IsActive())
                throw new DomainException("Можно отменить только активный ордер.");

            Status = OrderStatus.Cancelled;
        }

        public TradeOrder WithQuantity(int newQuantity)
        {
            if (newQuantity <= 0)
                throw new DomainException("Количество должно быть больше 0");

            return new TradeOrder
            {
                Id = Guid.NewGuid().ToString(), // Новый Id
                Quantity = newQuantity,
                FilledQuantity = 0, // Сбрасываем исполненное количество
                Price = Price,
                Type = Type,
                TraderTelegramId = TraderTelegramId,
                CharacterTokenId = CharacterTokenId,
                Status = OrderStatus.Active, // Новый статус
                CreatedAt = DateTime.UtcNow // Новое время создания
            };
        }
    }
}
