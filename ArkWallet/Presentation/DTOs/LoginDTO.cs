namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Запрос на аутентификацию через Telegram
    /// </summary>
    /// <param name="InitData">Строка InitData из Telegram WebApp</param>
    public record LoginRequest(string InitData);
    /// <summary>
    /// Ответ с JWT-токеном
    /// </summary>
    /// <param name="Token">JWT-токен для доступа к API</param>
    public record LoginResponse(string Token);
}
