using ArkWallet.Core.General.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Infrastructure.Data
{
    internal class CharacterToken : EntityData
    {
        [Key]
        public string Symbol { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public CharacterRarity Rarity { get; private set; }
        public decimal CurrentPrice { get; set; }
        public int TotalSupply { get; private set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public string ImageUrl { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;

        // Computed properties (правило 18: разрешены get_ свойства)
        public bool CanBeTraded => IsActive && TotalSupply > 0;
        public decimal CalculateMarketCap => CurrentPrice * TotalSupply;

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
    }
}