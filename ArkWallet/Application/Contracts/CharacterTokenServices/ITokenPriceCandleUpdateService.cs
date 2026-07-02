using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.CharacterTokenServices;

/// <summary>
/// Сервис для обновления ценовых свечей токена
/// </summary>
public interface ITokenPriceCandleUpdateService
{
    /// <summary>
    /// Обновляет ценовую свечу токена на основе новой цены
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="newPrice">Новая цена токена</param>
    /// <returns>Результат операции обновления</returns>
    /// <remarks>
    /// <para>
    /// Логика обновления свечей:
    /// - Если свечей нет → создаётся новая свеча
    /// - Если последняя свеча старше 1 минуты → создаётся новая свеча
    /// - Если последняя свеча младше 1 минуты → обновляется существующая
    /// </para>
    /// <para>
    /// При создании новой свечи:
    /// - OpenPrice = ClosePrice предыдущей свечи (или newPrice, если свечей нет)
    /// - HighPrice = LowPrice = ClosePrice = newPrice
    /// </para>
    /// <para>
    /// При обновлении существующей свечи:
    /// - HighPrice = max(HighPrice, newPrice)
    /// - LowPrice = min(LowPrice, newPrice)
    /// - ClosePrice = newPrice
    /// </para>
    /// </remarks>
    Task<Result> UpdateTokenPriceCandleAsync(string symbol, decimal newPrice);
}