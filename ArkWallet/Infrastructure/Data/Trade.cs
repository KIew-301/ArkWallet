using ValueObjects = global::ArkWallet.Core.General.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data
{
    internal class Trade : EntityData
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public long BuyerId { get; set; }
        public long SellerId { get; set; }
        public string CharacterTokenId { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        public virtual Trader Buyer { get; set; } = null!;
        public virtual Trader Seller { get; set; } = null!;
        public virtual CharacterToken CharacterToken { get; set; } = null!;

        /// <summary>Общая стоимость сделки</summary>
        public decimal GetTotalValue => Price * Quantity;

        /// <summary>Описание сделки в читаемом формате</summary>
        public string GetDescription => $"{Quantity} {CharacterTokenId} по {Price}{ValueObjects.Descriptor.CurrencySymbol}";
    }
}
