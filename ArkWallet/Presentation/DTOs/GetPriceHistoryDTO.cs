using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Запрос на получение истории свечей
    /// </summary>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="StartDateTimeOffset">Начало периода</param>
    /// <param name="EndDateTimeOffset">Конец периода</param>
    public record GetPriceHistoryRequest(string Symbol, DateTimeOffset StartDateTimeOffset, DateTimeOffset EndDateTimeOffset);
    /// <summary>
    /// Ответ со списком свечей
    /// </summary>
    /// <param name="Candles">Массив свечей</param>
    public record GetPriceHistoryResponse(PriceCandleInfo[] Candles);
}
