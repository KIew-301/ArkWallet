using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор для получения и агрегации свечей
/// </summary>
public interface ICandleOrchestrator
{
    /// <summary>
    /// Получает свечи за период и агрегирует их в указанный таймфрейм
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="startDateTime">Начало периода</param>
    /// <param name="endDateTime">Конец периода</param>
    /// <param name="timeframeMinutes">Таймфрейм в минутах (например, 5 для 5-минутных свечей)</param>
    /// <returns>Список агрегированных свечей</returns>
    Task<Result<List<PriceCandleInfo>>> GetAggregatedCandlesAsync(
        string symbol,
        DateTime startDateTime,
        DateTime endDateTime,
        int timeframeMinutes);
}