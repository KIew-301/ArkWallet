using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Presentation.DTOs
{
    public record GetPriceHistoryRequest(string Symbol, DateTimeOffset StartDateTimeOffset, DateTimeOffset EndDateTimeOffset);
    public record GetPriceHistoryResponse(PriceCandleInfo[] Candles);
}
