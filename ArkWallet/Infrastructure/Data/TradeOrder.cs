using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.General.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data
{
    /// <summary>
    /// Представляет торговый ордер на покупку или продажу токенов.
    /// Ордер может быть активным, исполненным или отменённым.
    /// </summary>
    internal class TradeOrder : EntityData
    {
        /// <summary>Уникальный идентификатор ордера (генерируется автоматически).</summary>
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Тип ордера: Buy (покупка) или Sell (продажа).</summary>
        public OrderType Type { get; set; }

        /// <summary>Текущий статус ордера: Active, Filled, Cancelled.</summary>
        public OrderStatus Status { get; set; } = OrderStatus.Active;

        /// <summary>Идентификатор токена, с которым работает ордер (например, "BTC", "ETH").</summary>
        public string CharacterTokenId { get; set; } = string.Empty;

        /// <summary>Telegram ID трейдера, разместившего ордер.</summary>
        public long TraderTelegramId { get; set; }

        /// <summary>Цена за один токен в базовой валюте.</summary>
        public decimal Price { get; set; }

        /// <summary>Средняя цена исполнения за один токен в базовой валюте.</summary>
        public decimal AverageExecutePrice { get; set; }

        /// <summary>Общее количество токенов в ордере.</summary>
        public int Quantity { get; set; }

        /// <summary>Количество токенов, уже исполненных по данному ордеру.</summary>
        public int FilledQuantity { get; set; } = 0;

        /// <summary>Дата и время создания ордера (UTC).</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Дата и время полного исполнения ордера (UTC). null, если ордер не исполнен.</summary>
        public DateTime? ExecutedAt { get; set; }

        /// <summary>Связанный трейдер (навигационное свойство).</summary>
        public virtual Trader? Trader { get; set; }

        /// <summary>Связанный токен (навигационное свойство).</summary>
        public virtual CharacterToken? CharacterToken { get; set; }

        // Computed properties (правило 18: разрешены get_ свойства)
        /// <summary>Полностью ли исполнен ордер</summary>
        public bool IsFilled => FilledQuantity >= Quantity;

        /// <summary>Активен ли ордер (не исполнен и не отменён)</summary>
        public bool IsActive => Status == OrderStatus.Active;

        /// <summary>Является ли ордер на покупку</summary>
        public bool IsLong => Type == OrderType.Buy;

        /// <summary>Является ли ордер на продажу</summary>
        public bool IsShort => Type == OrderType.Sell;

        /// <summary>Осталось токенов для исполнения</summary>
        public int RemainingQuantity => Quantity - FilledQuantity;

        /// <summary>Сумма, зарезервированная под оставшуюся часть ордера</summary>
        public decimal ReservedBalance => RemainingQuantity * Price;

        /// <summary>Создаёт новый ордер с валидацией входных параметров.</summary>
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
                Quantity = quantity,
                AverageExecutePrice = 0
            };
        }

        /// <summary>Отменяет ордер (если он активен и принадлежит отменяющему).</summary>
        public void Update(long initiatorTraderId)
        {
            if (!IsActive)
                throw new DomainException("Можно отменить только активный ордер.");

            if (TraderTelegramId != initiatorTraderId)
                throw new DomainException("Нельзя отменить чужой ордер.");

            Status = OrderStatus.Cancelled;
        }
    }
}