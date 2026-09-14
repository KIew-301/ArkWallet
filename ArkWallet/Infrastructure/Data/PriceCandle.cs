namespace ArkWallet.Infrastructure.Data
{
    internal class PriceCandle : EntityData
    {
        public long Id { get; internal set; }
        public decimal OpenPrice { get; internal set; }
        public decimal HighPrice { get; internal set; }
        public decimal LowPrice { get; internal set; }
        public decimal ClosePrice { get; internal set; }
        public DateTime Timestamp { get; internal set; }

        public string CharacterTokenId { get; internal set; } = string.Empty;

        public CharacterToken CharacterToken { get; internal set; } = null!;

        public static PriceCandle Create(string characterTokenId, decimal openPrice, DateTime ts)
        {
            return new PriceCandle
            {
                CharacterTokenId = characterTokenId,
                OpenPrice = openPrice,
                HighPrice = openPrice,
                LowPrice = openPrice,
                ClosePrice = openPrice,
                Timestamp = ts
            };
        }
    }
}
