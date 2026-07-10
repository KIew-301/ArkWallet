using ArkWallet.Application.Common;
namespace ArkWallet.Application.Contracts.CharacterTokenServices;

/// <summary>
/// Сервис для агрегации свечей в более крупные таймфреймы
/// </summary>
public interface ICandleAggregatorService
{
    /// <summary>
    /// Агрегирует массив свечей в указанный таймфрейм
    /// </summary>
    /// <param name="candles">Исходные свечи (должны быть отсортированы по времени)</param>
    /// <param name="timeframeMinutes">Количество минут в таймфрейме (например, 5 для 5-минутных свечей)</param>
    /// <returns>Результат с массивом агрегированных свечей</returns>
    /// <remarks>
    /// <para>
    /// Логика агрегации:
    /// - OpenPrice = цена открытия первой свечи в группе
    /// - HighPrice = максимальная HighPrice из всех свечей в группе
    /// - LowPrice = минимальная LowPrice из всех свечей в группе
    /// - ClosePrice = цена закрытия последней свечи в группе
    /// - DateTime = время первой свечи в группе
    /// - Timestamp = Unix timestamp времени первой свечи в группе
    /// </para>
    /// <para>
    /// Группировка выполняется по временным интервалам:
    /// - Например, для 5-минутного таймфрейма свечи группируются по 5 минут
    /// - Время группировки: 00-04, 05-09, 10-14 и т.д.
    /// </para>
    /// </remarks>
    Task<Result<List<PriceCandleInfo>>> AggregateAsync(List<PriceCandleInfo> candles, int timeframeMinutes);
}