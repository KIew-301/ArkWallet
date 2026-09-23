using ArkWallet.Core.TradingContext.Application.Contracts.Other;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Other;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace ArkWallet.Core.TradingContext.Application.Services.Other;
using static ArkWallet.Core.General.Application.Common.Result<TelegramInitData>;

internal class TraderAuthService(ILogger<TraderAuthService> logger) : ITraderAuthService
{
    private const int MaxAuthAgeSeconds = 86400; // 24 часа

    public Result<TelegramInitData> AuthenticateUser(string initDataJson, string? botToken)
    {
        return ServiceErrorHandler.Execute(() =>
        {
            if (string.IsNullOrEmpty(initDataJson) || initDataJson.Length < 10)
                return Fail("Некорректная строка авторизации");

            if (string.IsNullOrEmpty(botToken) || botToken.Length < 10)
                return Fail("Потерян токен бота");

            var authResult = IsTelegramAuthValid(initDataJson, botToken);

            if (!authResult.IsSuccess)
                return Fail(authResult.Message);

            var parts = HttpUtility.ParseQueryString(initDataJson);

            if (string.IsNullOrEmpty(parts["user"]))
                return Fail("Ошибка десериализации данных пользователя");

            var data = new TelegramInitData(
                JsonSerializer.Deserialize<TelegramUserData>(parts["user"]!)!,
                parts["auth_date"]!,
                parts["chat_instance"],
                parts["chat_type"]
            );

            return Ok(data);
        }, logger, nameof(TraderAuthService));
    }

    private static Result IsTelegramAuthValid(string initData, string botToken)
    {
        var parts = HttpUtility.ParseQueryString(initData);
        var hash = parts["hash"];

        if (hash == null)
            return Result.Fail("Хеш не обнаружен");

        if (!long.TryParse(parts["auth_date"], out var authDate))
            return Result.Fail("Дата аутентификации не обнаружена");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - authDate > MaxAuthAgeSeconds)
            return Result.Fail("Истекла дата аутентификации");

        var dataCheckArr = parts.AllKeys!
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => k!)
            .Where(k => k != "hash")
            .ToDictionary(k => k, k => parts[k] ?? string.Empty);

        var sorted = dataCheckArr.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}");
        string dataCheckString = string.Join("\n", sorted);

        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var trueCash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));

        var trueCashHex = Convert.ToHexStringLower(trueCash);

        if (trueCashHex == hash)
            return Result.Ok();
        else
            return Result.Fail("Данные недействительны");
    }
}

/// <summary>Данные пользователя Telegram, полученные из init_data.</summary>
/// <param name="Id">Уникальный идентификатор пользователя.</param>
/// <param name="FirstName">Имя пользователя (отображается в чатах).</param>
/// <param name="LastName">Фамилия пользователя (может отсутствовать).</param>
/// <param name="Username">Юзернейм пользователя (может отсутствовать).</param>
/// <param name="LanguageCode">Код языка пользователя (например, "ru").</param>
/// <param name="IsPremium">Флаг наличия Premium-подписки.</param>
/// <param name="AllowsWriteToPm">Разрешено ли отправлять сообщения от имени бота напрямую пользователю.</param>
/// <param name="PhotoUrl">URL фотографии профиля пользователя (может отсутствовать).</param>
public record TelegramUserData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("language_code")] string? LanguageCode,
    [property: JsonPropertyName("is_premium")] bool IsPremium,
    [property: JsonPropertyName("allows_write_to_pm")] bool AllowsWriteToPm,
    [property: JsonPropertyName("photo_url")] string? PhotoUrl
);

/// <summary>Результат парсинга init_data Telegram — данные для аутентификации.</summary>
/// <param name="User">Данные пользователя Telegram из инициализационных данных.</param>
/// <param name="AuthDate">Временная метка (Unix timestamp) момента инициализации.</param>
/// <param name="ChatInstance">Уникальный идентификатор чата/контекста (может отсутствовать).</param>
/// <param name="ChatType">Тип чата: "private"/"sender"/"group"/"supergroup"/"channel".</param>
public record TelegramInitData(
    TelegramUserData User,
    string AuthDate,
    string? ChatInstance,
    string? ChatType
);
