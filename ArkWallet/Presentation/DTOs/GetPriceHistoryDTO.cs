using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Запрос на получение истории свечей
    /// </summary>
    /// <param name="Symbol">Символ токена (например, "ZZZ")</param>
    /// <param name="StartDateTimeOffset">Начало периода (ISO 8601, например, 2026-07-08T00:00:00Z)</param>
    /// <param name="EndDateTimeOffset">Конец периода (ISO 8601, например, 2026-07-09T00:00:00Z)</param>
    /// <param name="TimeFrameInMinutes">Таймфрейм в минутах (1, 5, 15, 30, 60). По умолчанию 1</param>
    public record GetPriceHistoryRequest(
        string Symbol,
        DateTimeOffset StartDateTimeOffset,
        DateTimeOffset EndDateTimeOffset,
        int TimeFrameInMinutes
    );
    /// <summary>
    /// Ответ со списком свечей
    /// </summary>
    /// <param name="Candles">Массив свечей</param>
    public record GetPriceHistoryResponse(PriceCandleInfo[] Candles);
}
