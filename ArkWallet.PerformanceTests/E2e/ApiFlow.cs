using ArkWallet.Application.Services.Other;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;

namespace ArkWallet.PerformanceTests.E2e;

internal sealed class ApiFlow
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public ApiFlow(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private string BotToken => _config["Telegram:BotToken:Main"]!;

    public async Task<string> LoginAsync(long telegramId, string firstName = "E2E User")
    {
        var initData = BuildInitData(BotToken, telegramId, firstName);
        using var response = await _http.PostAsJsonAsync("/api/v1/auth/login", new { initData });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseBody>();
        return body!.Token;
    }

    public void Authorize(string token)
        => _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static string BuildInitData(string botToken, long telegramId, string firstName)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var userJson = JsonSerializer.Serialize(new TelegramUserData(
            telegramId, firstName, null, "e2e_user", "ru", false, false, null));
        var checkString = $"auth_date={authDate}\nuser={userJson}";

        var secret = Hmac("WebAppData", botToken);
        var hash = Convert.ToHexStringLower(Hmac(secret, checkString));

        return $"auth_date={authDate}&user={HttpUtility.UrlEncode(userJson)}&hash={hash}";
    }

    private static byte[] Hmac(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static byte[] Hmac(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private sealed record LoginResponseBody(string Token);
}
