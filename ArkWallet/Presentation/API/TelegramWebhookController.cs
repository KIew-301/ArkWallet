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
    /// <summary>
    /// Приём апдейта от Telegram Bot API (вебхук): Telegram делает POST на этот адрес
    /// при каждом новом сообщении/callback.
    /// </summary>
    /// <param name="secretToken">Секретный токен из заголовка X-Telegram-Bot-Api-Secret-Token (проверяется, если настроен)</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>200 при успешном приёме апдейта</returns>
    /// <response code="200">Апдейт принят и передан в фоновую обработку</response>
    /// <response code="401">Неверный X-Telegram-Bot-Api-Secret-Token</response>
    /// <response code="400">Некорректное тело запроса (нельзя десериализовать Update)</response>
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Post(
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
        CancellationToken cancellationToken)
    {
        string? webhookSecret = configuration["Telegram:WebhookSecret"];

        if (!string.IsNullOrWhiteSpace(webhookSecret) &&
            !string.Equals(secretToken, webhookSecret, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        // Тело десериализуем вручную через JsonBotAPI.Options: Telegram шлёт snake_case
        // (message_id, first_name...), а стандартный биндинг тела этого не понимает.
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