using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.CharacterTokenServices;

/// <summary>
/// Сервис для обновления данных токена
/// </summary>
internal interface ITokenMediaUpdateService
{
    /// <summary>
    /// Обновляет иконку и изображение токена
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="iconUrl">URL иконки</param>
    /// <param name="imageUrl">URL изображения</param>
    Task<Result> UpdateTokenMediaAsync(string symbol, string iconUrl, string imageUrl);
}
