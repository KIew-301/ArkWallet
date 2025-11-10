using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Entities
{
    internal class Trade
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Участники сделки
        public long BuyerId { get; set; }           // Покупатель
        public long SellerId { get; set; }          // Продавец
        public string CharacterTokenId { get; set; } // Какой токен

        // Детали сделки
        public decimal Price { get; set; }          // Цена исполнения
        public int Quantity { get; set; }           // Количество
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual Trader Buyer { get; set; }
        public virtual Trader Seller { get; set; }
        public virtual CharacterToken CharacterToken { get; set; }

        // Методы
        public decimal GetTotalValue() => Price * Quantity;

        public bool InvolvesTrader(long telegramId)
            => BuyerId == telegramId || SellerId == telegramId;

        public string GetDescription()
        {
            return $"{Quantity} {CharacterTokenId} по {Price}₽";
        }
    }
}
