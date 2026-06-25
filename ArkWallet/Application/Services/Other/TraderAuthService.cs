using ArkWallet.Application.Common;
using ArkWallet.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace ArkWallet.Application.Services.Other;
using static ArkWallet.Application.Common.Result<TelegramInitData>;

internal class TraderAuthService(ILogger<TraderAuthService> logger)
{
    private const int MaxAuthAgeSeconds = 86400; // 24 часа

    public Result<TelegramInitData> AuthenticateUser(string initDataJson, string? botToken)
    {
        try
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
        }
        catch (DomainException ex)
        {
            return Fail($"Ошибка бизнес-логики: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Ошибка аутентификации трейдера с данными:\n{initDataJson}");
            return Fail("Внутренняя ошибка сервера");
        }
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

        Dictionary<string, string> dataCheckArr = new();

        foreach (var key in parts.AllKeys)
            if (key != null && key != "hash")
                dataCheckArr.Add(key, parts[key]);

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

public record TelegramInitData(
    TelegramUserData User,
    string AuthDate,
    string? ChatInstance,
    string? ChatType
);
