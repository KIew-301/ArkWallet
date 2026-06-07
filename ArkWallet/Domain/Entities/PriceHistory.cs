using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Domain.Entities
{
    internal class PriceCandle
    {
        public long Id { get; private set; }
        public decimal OpenPrice { get; private set; }
        public decimal HighPrice { get; private set; }
        public decimal LowPrice { get; private set; }
        public decimal ClosePrice { get; private set; }
        public DateTime Timestamp { get; private set; }

        public string CharacterTokenId { get; private set; }

        public CharacterToken CharacterToken { get; set; }

        public static PriceCandle CreateNew(string characterTokenId, decimal openPrice, DateTime ts)
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

        public void Update(decimal newPrice)
        {
            if (newPrice > HighPrice)
                HighPrice = newPrice;
            if (newPrice < LowPrice)
                LowPrice = newPrice;
            ClosePrice = newPrice;
        }
    }
}
