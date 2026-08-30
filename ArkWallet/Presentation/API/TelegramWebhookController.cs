using ArkWallet.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Приём апдейтов Telegram Bot API через вебхук: Telegram делает POST на этот адрес
/// при каждом новом сообщении/callback. Поллинг (getUpdates) при этом не используется.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Webhook-контроллер: точка входа Telegram Bot API извне, зависит от внешнего клиента. Тестируется интеграционно.")]
[ApiController]
[AllowAnonymous]
[Route("bot/webhook")]
public class TelegramWebhookController(TelegramBot telegramBot, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        string? webhookSecret = configuration["Telegram:WebhookSecret"];

        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            var secretHeader = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
            if (!string.Equals(secretHeader, webhookSecret, StringComparison.Ordinal))
                return Unauthorized();
        }

        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        Update? update;
        try
        {
            update = JsonSerializer.Deserialize<Update>(body, JsonBotAPI.Options);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (update is null)
            return BadRequest();

        // Обработка уходит в фоновую очередь (ScheduleUpdate), а токен запроса
        // (RequestAborted) отменяется сразу после отправки 200. Передавать его нельзя —
        // иначе фоновый обработчик и его отправки в Telegram будут отменены.
        await telegramBot.HandleWebhookUpdateAsync(update, CancellationToken.None);

        return Ok();
    }
}