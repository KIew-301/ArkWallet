using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record GetPriceHistoryRequest(string Symbol, int PeriodDays);
    public record GetPriceHistoryResponse(Candle[] Candles);
    public record Candle(DateTime Timestamp, decimal OpenPrice, decimal LowPrice, decimal HighPrice, decimal ClosePrice);
}
