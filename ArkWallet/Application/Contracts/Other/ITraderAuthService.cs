using ArkWallet.Application.Common;
using ArkWallet.Application.Services.Other;

namespace ArkWallet.Application.Contracts.Other;

/// <summary>
/// Сервис для аутентификации пользователей через Telegram WebApp InitData
/// </summary>
public interface ITraderAuthService
{
    /// <summary>
    /// Аутентифицирует пользователя по данным из Telegram WebApp
    /// </summary>
    /// <param name="initDataJson">Строка InitData из Telegram WebApp</param>
    /// <param name="botToken">Токен бота для проверки подписи</param>
    /// <returns>Результат аутентификации с данными пользователя</returns>
    /// <remarks>
    /// <para>
    /// Выполняет проверку:
    /// - Валидация строки InitData
    /// - Проверка хеша (HMAC-SHA256) на основе токена бота
    /// - Проверка времени жизни (не старше 24 часов)
    /// - Десериализация данных пользователя
    /// </para>
    /// </remarks>
    Result<TelegramInitData> AuthenticateUser(string initDataJson, string? botToken);
}