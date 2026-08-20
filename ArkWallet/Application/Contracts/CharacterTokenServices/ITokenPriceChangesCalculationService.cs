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

    /// <summary>
    /// Рассчитывает процентное изменение цены между свечой с указанной позицией с конца и последней свечой для набора символов
    /// </summary>
    /// <param name="symbols">Символы токенов</param>
    /// <param name="candlePosition">Позиция целевой свечи с конца (больше нуля; 1 — предпоследняя свеча)</param>
    /// <returns>Словарь процентных изменений цены по символам</returns>
    /// <remarks>
    /// <para>
    /// Расчёт выполняется только на основе свечей из таблицы PriceCandles без обращения к текущей цене токена.
    /// </para>
    /// <para>
    /// Для каждого символа берутся две свечи:
    /// - Последняя свеча (закрытие)
    /// - Свеча с позицией candlePosition с конца (открытие)
    /// </para>
    /// <para>
    /// Изменение в процентах: (close последней свечи - open целевой свечи) / open целевой свечи * 100.
    /// Символы без достаточной истории или с нулевой ценой целевой свечи пропускаются.
    /// </para>
    /// </remarks>
    Task<Dictionary<string, decimal>> TakeSymbolsPriceChangesAsync(string[] symbols, int candlePosition);
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