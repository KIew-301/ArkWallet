using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities
{
    /// <summary>
    /// Представляет торговый ордер на покупку или продажу токенов.
    /// Ордер может быть активным, исполненным или отменённым.
    /// </summary>
    internal class TradeOrder
    {
        /// <summary>Уникальный идентификатор ордера (генерируется автоматически).</summary>
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Тип ордера: Buy (покупка) или Sell (продажа).</summary>
        public OrderType Type { get; set; }

        /// <summary>Текущий статус ордера: Active, Filled, Cancelled.</summary>
        public OrderStatus Status { get; set; } = OrderStatus.Active;

        /// <summary>Идентификатор токена, с которым работает ордер (например, "BTC", "ETH").</summary>
        public string CharacterTokenId { get; set; }

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

        /// <summary>Проверяет, полностью ли исполнен ордер.</summary>
        public bool IsFilled() => FilledQuantity >= Quantity;

        /// <summary>Проверяет, активен ли ордер (не исполнен и не отменён).</summary>
        public bool IsActive() => Status == OrderStatus.Active;

        /// <summary>Проверяет, является ли ордер на покупку.</summary>
        public bool IsLong() => Type == OrderType.Buy;

        /// <summary>Проверяет, является ли ордер на продажу.</summary>
        public bool IsShort() => Type == OrderType.Sell;

        /// <summary>Проверяет, принадлежит ли ордер указанному трейдеру.</summary>
        public bool IsTraderOrder(long initiatorId) => TraderTelegramId == initiatorId;

        /// <summary>Возвращает оставшееся количество токенов, ещё не исполненных по ордеру.</summary>
        public int GetRemainingQuantity() => Quantity - FilledQuantity;

        /// <summary>Возвращает сумму, зарезервированную под оставшуюся часть ордера (цена × остаток).</summary>
        public decimal GetReservedBalance() => GetRemainingQuantity() * Price;

        /// <summary>Отмечает ордер как полностью исполненный.</summary>
        public void MarkAsFilled()
        {
            Status = OrderStatus.Filled;
            ExecutedAt = DateTime.UtcNow;
            FilledQuantity = Quantity;
        }

        /// <summary>
        /// Создаёт новый экземпляр ордера с валидацией входных параметров.
        /// </summary>
        /// <param name="orderType">Тип ордера (Buy/Sell).</param>
        /// <param name="symbol">Символ токена.</param>
        /// <param name="traderId">Telegram ID трейдера.</param>
        /// <param name="price">Цена за токен (должна быть > 0).</param>
        /// <param name="quantity">Количество токенов (должно быть > 0).</param>
        /// <returns>Новый активный ордер.</returns>
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

        /// <summary>
        /// Заполняет ордер.
        /// </summary>
        /// <param name="filledQuantity">На какое количество токенов заполнен ордер.</param>
        /// <param name="price">По какой цене заполнен ордер.</param>
        public void UpdateOrderFill(int filledQuantity, decimal price)
        {
            var totalCost = FilledQuantity * AverageExecutePrice + filledQuantity * price;
            FilledQuantity += filledQuantity;
            AverageExecutePrice = totalCost / FilledQuantity;

            if (IsFilled())
                MarkAsFilled();
        }

        /// <summary>
        /// Отменяет ордер (если он активен и принадлежит указанному трейдеру).
        /// </summary>
        /// <param name="initiatorId">Telegram ID инициатора отмены.</param>
        public void Cancel(long initiatorId)
        {
            if (!IsTraderOrder(initiatorId))
                throw new DomainException("Нельзя отменить чужой ордер.");

            if (!IsActive())
                throw new DomainException("Можно отменить только активный ордер.");

            Status = OrderStatus.Cancelled;
        }

        /// <summary>
        /// Создаёт новый ордер с уменьшенным количеством токенов.
        /// Используется для частичного исполнения, когда ордер не полностью заполнен.
        /// </summary>
        /// <param name="newQuantity">Новое количество токенов (> 0).</param>
        /// <returns>Новый активный ордер с обновлённым количеством и сброшенным FilledQuantity.</returns>
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