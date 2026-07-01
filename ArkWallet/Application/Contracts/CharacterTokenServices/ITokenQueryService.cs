using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.CharacterTokenServices;

/// <summary>
/// Сервис для получения данных о токенах
/// </summary>
public interface ITokenQueryService
{
    /// <summary>
    /// Возвращает список всех активных токенов
    /// </summary>
    /// <returns>Список токенов с основной информацией</returns>
    /// <remarks>
    /// <para>
    /// Возвращает только активные токены (IsActive = true).
    /// Информация включает:
    /// - Символ токена
    /// - Название
    /// - Текущая цена
    /// - Общее количество в обращении
    /// - Статус активности
    /// </para>
    /// </remarks>
    Task<Result<List<TokenInfo>>> GetAllActiveTokensAsync();
}

/// <summary>
/// DTO с информацией о токене для отображения на клиенте
/// </summary>
/// <param name="Symbol">Уникальный символ токена</param>
/// <param name="Name">Название токена</param>
/// <param name="CurrentPrice">Текущая цена токена</param>
/// <param name="DailyChangePercent">Процентное изменение цены за последние 24 часа</param>
/// <param name="IconUrl">Ссылка на иконку токена (маленькая)</param>
/// <param name="ImageUrl">Ссылка на изображение токена (полноразмерное)</param>
public record TokenInfo(
    string Symbol,
    string Name,
    decimal CurrentPrice,
    decimal DailyChangePercent,
    string IconUrl,
    string ImageUrl
)
{
    static internal TokenInfo FromEntity(CharacterToken token, decimal dailyChangePercent)
    {
        return new TokenInfo(
            token.Symbol,
            token.Name,
            token.CurrentPrice,
            dailyChangePercent,
            token.IconUrl,
            token.ImageUrl
        );
    }
}