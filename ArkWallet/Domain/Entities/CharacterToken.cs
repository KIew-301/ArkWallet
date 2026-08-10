using ArkWallet.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities
{
    internal class CharacterToken
    {
        [Key]
        public string Symbol { get; private set; }
        public string Name { get; private set; }
        public CharacterRarity Rarity { get; private set; }
        public decimal CurrentPrice { get; private set; }
        public int TotalSupply { get; private set; }
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public string ImageUrl { get; private set; }
        public string IconUrl { get; private set; }

        public static CharacterToken Create(
            string symbol,
            string name,
            CharacterRarity rarity,
            decimal initialPrice,
            int totalSupply,
            string imageUrl,
            string iconUrl)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Символ токена не может быть пустым", nameof(symbol));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название токена не может быть пустым", nameof(name));

            if (initialPrice <= 0)
                throw new ArgumentException("Начальная цена должна быть больше нуля", nameof(initialPrice));

            if (totalSupply <= 0)
                throw new ArgumentException("Общее количество должно быть больше нуля", nameof(totalSupply));

            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("Ссылка на изображение не может быть пустой", nameof(imageUrl));

            if (string.IsNullOrWhiteSpace(iconUrl))
                throw new ArgumentException("Ссылка на иконку не может быть пустой", nameof(iconUrl));

            return new CharacterToken
            {
                Symbol = symbol,
                Name = name,
                Rarity = rarity,
                CurrentPrice = initialPrice,
                TotalSupply = totalSupply,
                ImageUrl = imageUrl,
                IconUrl = iconUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public bool CanBeTraded() => IsActive && TotalSupply > 0;

        public void Deactivate() => IsActive = false;

        public void Activate() => IsActive = true;

        public void UpdatePrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException("Price cannot be negative");
            CurrentPrice = newPrice;
        }

        public void UpdateMedia(string iconUrl, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(iconUrl))
                throw new ArgumentException("Icon URL cannot be empty");
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("Image URL cannot be empty");

            IconUrl = iconUrl;
            ImageUrl = imageUrl;
        }

        public decimal CalculateMarketCap() => CurrentPrice * TotalSupply;
    }
}