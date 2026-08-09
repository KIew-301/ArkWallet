using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;

namespace ArkWallet.Presentation.Health;

[ExcludeFromCodeCoverage(Justification = "Инфраструктурная middleware, покрывается интеграционным/смоук-тестом")]
internal sealed class MetricsApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string? _apiKey = configuration["Metrics:ApiKey"];
    private const string MetricsPath = "/metrics";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(MetricsPath, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(_apiKey) || !IsAuthorized(context)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out StringValues authHeader))
            return false;

        var token = authHeader.ToString();
        return token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && token[7..].Trim() == _apiKey;
    }
}
