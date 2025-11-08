using ArkWallet.ValueObjects;

namespace ArkWallet.Entities
{
    internal class CharacterToken
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Symbol { get; set; }                     
        public string Name { get; set; }
        public CharacterRarity Rarity { get; set; }
        public decimal CurrentPrice { get; set; }
        public int TotalSupply { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool CanBeTraded() => IsActive && TotalSupply > 0;

        public void UpdatePrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException("Price cannot be negative");
            CurrentPrice = newPrice;
        }

        public decimal CalculateMarketCap() => CurrentPrice * TotalSupply;
    }
}
