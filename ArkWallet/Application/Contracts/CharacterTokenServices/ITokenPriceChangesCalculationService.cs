using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.CharacterTokenServices;

/// <summary>
/// Сервис для расчёта изменений цены токена за период
/// </summary>
public interface ITokenPriceChangesCalculationService
{
    /// <summary>
    /// Рассчитывает изменения цены токена за указанный период
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="periodDays">Количество дней для расчёта (минимум 1)</param>
    /// <returns>Результат с данными об изменении цены</returns>
    /// <remarks>
    /// <para>
    /// Расчёт выполняется на основе:
    /// - Текущей цены токена (из таблицы CharacterTokens)
    /// - Исторической цены за указанный период (из таблицы PriceCandles)
    /// </para>
    /// <para>
    /// Используются данные из последней свечи за период.
    /// Если историческая цена отсутствует, используется значение по умолчанию (0).
    /// </para>
    /// </remarks>
    Task<Result<TokenPriceChangesData>> TakeTokenPriceChangesAsync(string symbol, int periodDays);
}

/// <summary>
/// Данные об изменении цены токена
/// </summary>
/// <param name="CurrentPrice">Текущая цена</param>
/// <param name="PreviousPrice">Цена за указанный период</param>
/// <param name="ChangeAbsolute">Абсолютное изменение цены</param>
/// <param name="ChangePercent">Процентное изменение цены</param>
public record TokenPriceChangesData(
    decimal CurrentPrice,
    decimal PreviousPrice,
    decimal ChangeAbsolute,
    decimal ChangePercent
);