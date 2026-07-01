using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.CharacterTokenServices;

/// <summary>
/// Сервис для получения данных о свечах токена
/// </summary>
public interface ITokenPriceCandleQueryService
{
    /// <summary>
    /// Возвращает список свечей токена за указанный период
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="startDateTime">Начало периода (включительно)</param>
    /// <param name="endDateTime">Конец периода (исключительно)</param>
    /// <returns>Список свечей с информацией</returns>
    /// <remarks>
    /// <para>
    /// Возвращает свечи в хронологическом порядке (по возрастанию Timestamp).
    /// </para>
    /// </remarks>
    Task<Result<List<PriceCandleInfo>>> GetPriceCandlesAsync(
        string symbol,
        DateTime startDateTime,
        DateTime endDateTime);
}

/// <summary>
/// DTO с информацией о свече для отображения на клиенте
/// </summary>
/// <param name="OpenPrice">Цена открытия</param>
/// <param name="HighPrice">Максимальная цена</param>
/// <param name="LowPrice">Минимальная цена</param>
/// <param name="ClosePrice">Цена закрытия</param>
/// <param name="Timestamp">Время свечи</param>
public record PriceCandleInfo(
    decimal OpenPrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal ClosePrice,
    DateTime Timestamp
)
{
    internal static PriceCandleInfo FromEntity(PriceCandle candle)
    {
        return new PriceCandleInfo(
            candle.OpenPrice,
            candle.HighPrice,
            candle.LowPrice,
            candle.ClosePrice,
            candle.Timestamp
        );
    }
}