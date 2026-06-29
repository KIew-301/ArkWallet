using ArkWallet.Application.Services.Other;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ArkWallet.Tests.ServiceTests;

public class TraderAuthServiceTest
{
    [Fact]
    public void AuthTrader_WithValidData_ReturnsSuccess()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";
        var telegramId = 101;
        var authDateTime = DateTimeOffset.UtcNow;

        var initData = GenerateValidInitData(botToken, telegramId, authDateTime);
        var result = service.AuthenticateUser(initData, botToken);

        Assert.True(result.TryGetData(out var data));

        Assert.Equal(101, data.User.Id);
        Assert.Equal("Test", data.User.FirstName);
        Assert.Equal("User", data.User.LastName);
        Assert.Equal("testuser", data.User.Username);
        Assert.Equal("ru", data.User.LanguageCode);
        Assert.False(data.User.IsPremium);

        Assert.Equal(authDateTime.ToUnixTimeSeconds().ToString(), data.AuthDate);
        Assert.Equal("1234567890123456789", data.ChatInstance);
        Assert.Equal("private", data.ChatType);
    }

    [Fact]
    public void AuthTrader_FakeInitData_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";

        var initData = "user=%7b%22id%22%3a777%2c%22first_name%22%3a%22Test%22%2c%22last_name%22%" +
            "3a%22User%22%2c%22username%22%3a%22testuser%22%2c%22language_code%22%3a%22ru%22%2c%22is_premium%22%" +
            "3afalse%2c%22allows_write_to_pm%22%3atrue%2c%22photo_url%22%3a%22https%" +
            "3a%2f%2ft.me%2fi%2fuserpic%2f123.jpg%22%7d&auth_date=5782410033&chat_instance=1234567890123456789&" +
            "chat_type=private&hash=25e386173eff2d0d8de01ee74269c6fc6409403ae89a23a453ba2982064a8230";
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Данные недействительны", result.Message);
    }

    [Fact]
    public void AuthTrader_EmptyInitData_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";

        var initData = "";
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Некорректная строка авторизации", result.Message);
    }

    [Fact]
    public void AuthTrader_MissBotToken_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "";
        var telegramId = 101;
        var authDateTime = DateTimeOffset.UtcNow;

        var initData = GenerateValidInitData(botToken, telegramId, authDateTime);
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Потерян токен бота", result.Message);
    }

    [Fact]
    public void AuthTrader_WithoutUserData_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";
        var authDateTime = DateTimeOffset.UtcNow;

        var initData = GenerateInitDataWithoutUserData(botToken, authDateTime);
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ошибка десериализации данных пользователя", result.Message);
    }

    [Fact]
    public void AuthTrader_WithoutAuthDate_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";
        var telegramId = 101;

        var initData = GenerateInitDataWithoutAuthDate(botToken, telegramId);
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Дата аутентификации не обнаружена", result.Message);
    }

    [Fact]
    public void AuthTrader_ExpiredAuthDate_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";
        var telegramId = 101;
        var authDateTime = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(-1);

        var initData = GenerateValidInitData(botToken, telegramId, authDateTime);
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Истекла дата аутентификации", result.Message);
    }

    [Fact]
    public void AuthTrader_WithoutHash_ReturnsFail()
    {
        var logger = NullLogger<TraderAuthService>.Instance;
        var service = new TraderAuthService(logger);
        var botToken = "test_bot_token";
        var telegramId = 101;
        var authDateTime = DateTimeOffset.UtcNow.AddDays(-1);

        var initData = GenerateInitDataWithoutHash(telegramId, authDateTime);
        var result = service.AuthenticateUser(initData, botToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Хеш не обнаружен", result.Message);
    }

    private static string BuildInitData(string botToken, Dictionary<string, string> parameters)
    {
        var dataCheckString = string.Join("\n", parameters
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}"));

        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(botToken)
        );
        var hash = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes(dataCheckString)
        );
        var hashHex = Convert.ToHexStringLower(hash);

        parameters["hash"] = hashHex;

        return string.Join("&", parameters
            .Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
    }

    private static string GenerateValidInitData(
        string botToken,
        long userId,
        DateTimeOffset authDateTimeOffset
    )
    {
        var userJson = CreateUserJson(userId);
        var parameters = CreateBaseParameters(userJson, authDateTimeOffset);
        return BuildInitData(botToken, parameters);
    }

    private static string GenerateInitDataWithoutUserData(
        string botToken,
        DateTimeOffset authDateTimeOffset
    )
    {
        var parameters = new Dictionary<string, string>
        {
            ["auth_date"] = authDateTimeOffset.ToUnixTimeSeconds().ToString()
        };
        return BuildInitData(botToken, parameters);
    }

    private static string GenerateInitDataWithoutAuthDate(
        string botToken,
        long userId
    )
    {
        var userJson = CreateUserJson(userId);
        var parameters = new Dictionary<string, string>
        {
            ["user"] = userJson
        };
        return BuildInitData(botToken, parameters);
    }

    private static string GenerateInitDataWithoutHash(
        long userId,
        DateTimeOffset authDateTimeOffset
    )
    {
        var userJson = CreateUserJson(userId);
        var parameters = CreateBaseParameters(userJson, authDateTimeOffset);

        return string.Join("&", parameters
            .Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
    }

    private static string CreateUserJson(long userId)
    {
        var userData = new
        {
            id = userId,
            first_name = "Test",
            last_name = "User",
            username = "testuser",
            language_code = "ru",
            is_premium = false,
            allows_write_to_pm = true,
            photo_url = "https://t.me/i/userpic/123.jpg"
        };

        return JsonSerializer.Serialize(userData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private static Dictionary<string, string> CreateBaseParameters(string userJson, DateTimeOffset authDateTimeOffset)
    {
        return new Dictionary<string, string>
        {
            ["user"] = userJson,
            ["auth_date"] = authDateTimeOffset.ToUnixTimeSeconds().ToString(),
            ["chat_instance"] = "1234567890123456789",
            ["chat_type"] = "private"
        };
    }
}