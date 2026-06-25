namespace ArkWallet.Application.Contracts.Other;

/// <summary>
/// Сервис для генерации JWT-токенов
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Генерирует JWT-токен для пользователя
    /// </summary>
    /// <param name="userTelegramId">Telegram ID пользователя</param>
    /// <returns>JWT-токен в виде строки</returns>
    /// <remarks>
    /// <para>
    /// Токен содержит:
    /// - Claim с Telegram ID пользователя
    /// - Время жизни: 7 дней
    /// - Подпись: HMAC-SHA256
    /// </para>
    /// </remarks>
    string GenerateToken(long userTelegramId);
}