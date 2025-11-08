namespace ArkWallet.Entities
{
    internal class PortfolioItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Внешние ключи
        public long TraderTelegramId { get; set; }
        public string CharacterTokenId { get; set; }

        // Данные владения
        public int Quantity { get; set; }
        public decimal AverageBuyPrice { get; set; }
        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual Trader Trader { get; set; }
        public virtual CharacterToken CharacterToken { get; set; }

        // Методы
        public decimal GetTotalValue()
            => Quantity * AverageBuyPrice;

        public decimal GetCurrentValue(CharacterToken token)
            => Quantity * token.CurrentPrice;

        public decimal GetProfitLoss(CharacterToken token)
            => GetCurrentValue(token) - GetTotalValue();
    }
}
