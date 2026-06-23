namespace ArkWallet.Presentation.DTOs
{
    public record GetPriceHistoryRequest(string Symbol, int PeriodDays);
    public record GetPriceHistoryResponse(Candle[] Candles);
    public record Candle(DateTime Timestamp, decimal OpenPrice, decimal LowPrice, decimal HighPrice, decimal ClosePrice);
}
