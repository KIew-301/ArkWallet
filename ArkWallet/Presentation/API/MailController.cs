using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для работы с письмами трейдера
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/[controller]")]
public class MailController(
    IQueryService mailQueryService,
    IStatusUpdatingService mailStatusUpdatingService) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private bool TryGetTraderId(out long traderId)
        => long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out traderId);

    /// <summary>
    /// Получение страницы писем трейдера, свежие первыми
    /// </summary>
    /// <param name="page">Номер страницы (начиная с 1)</param>
    /// <param name="pageSize">Размер страницы (по умолчанию 20, максимум 100)</param>
    /// <param name="filter">Фильтр писем: all, unread или reward</param>
    /// <returns>Страница писем с метаданными пагинации</returns>
    /// <response code="200">Письма успешно получены</response>
    /// <response code="400">Некорректные параметры пагинации или фильтра</response>
    /// <response code="401">Пользователь не авторизован</response>
    [ProducesResponseType(typeof(GetMailsResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMails(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? filter = null)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        if (page < 1)
            return BadRequest("Номер страницы должен быть больше или равен 1");

        if (pageSize is < 1 or > MaxPageSize)
            return BadRequest($"Размер страницы должен быть в диапазоне от 1 до {MaxPageSize}");

        if (!TryParseFilter(filter, out var mailFilter))
            return BadRequest("Фильтр должен быть одним из значений: all, unread, reward");

        var result = await mailQueryService.GetUserMailsPageAsync(traderId, mailFilter, page, pageSize);
        if (!result.TryGetData(out var pageResult))
            return BadRequest(result.Message);

        return Ok(new GetMailsResponse(
            pageResult.Items.ToArray(),
            pageResult.Page,
            pageResult.PageSize,
            pageResult.TotalCount,
            pageResult.TotalPages,
            pageResult.HasNext,
            pageResult.HasPrevious));
    }

    /// <summary>
    /// Получение счётчиков писем трейдера для вкладок фильтра
    /// </summary>
    /// <returns>Количество непрочитанных писем, писем с наградой и всего писем</returns>
    /// <response code="200">Счётчики успешно получены</response>
    /// <response code="400">Ошибка получения данных</response>
    /// <response code="401">Пользователь не авторизован</response>
    [ProducesResponseType(typeof(GetMailCountersResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [Authorize]
    [HttpGet("counters")]
    public async Task<IActionResult> GetCounters()
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await mailQueryService.GetMailCountersAsync(traderId);
        if (!result.TryGetData(out var counters))
            return BadRequest(result.Message);

        return Ok(new GetMailCountersResponse(
            counters.UnreadCount,
            counters.RewardCount,
            counters.TotalCount));
    }

    /// <summary>
    /// Получение письма трейдера по идентификатору
    /// </summary>
    /// <param name="id">Идентификатор письма</param>
    /// <returns>Письмо трейдера</returns>
    /// <response code="200">Письмо успешно получено</response>
    /// <response code="404">Письмо не найдено</response>
    /// <response code="401">Пользователь не авторизован</response>
    [ProducesResponseType(typeof(MailInfo), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetMail(long id)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await mailQueryService.GetUserMailAsync(traderId, id);
        if (!result.TryGetData(out var mail))
            return NotFoundMail(result.Message);

        return Ok(mail);
    }

    /// <summary>
    /// Пометить письмо как прочитанное
    /// </summary>
    /// <param name="id">Идентификатор письма</param>
    /// <returns>Обновлённое письмо</returns>
    /// <response code="200">Письмо помечено как прочитанное</response>
    /// <response code="404">Письмо не найдено</response>
    /// <response code="401">Пользователь не авторизован</response>
    [ProducesResponseType(typeof(MailInfo), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [Authorize]
    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await mailStatusUpdatingService.MarkAsReadAsync(id, traderId);
        if (!result.IsSuccess)
            return NotFoundMail(result.Message);

        return await GetUpdatedMailAsync(traderId, id);
    }

    /// <summary>
    /// Принять награду из письма
    /// </summary>
    /// <param name="id">Идентификатор письма</param>
    /// <returns>Обновлённое письмо с собранной наградой</returns>
    /// <response code="200">Награда принята</response>
    /// <response code="404">Письмо не найдено</response>
    /// <response code="409">Награда уже собрана или недоступна</response>
    /// <response code="401">Пользователь не авторизован</response>
    [ProducesResponseType(typeof(MailInfo), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(401)]
    [Authorize]
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> AcceptMail(long id)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await mailStatusUpdatingService.MarkAsAcceptedAsync(id, traderId);
        if (!result.IsSuccess)
            return ConflictOrNotFound(result.Message);

        return await GetUpdatedMailAsync(traderId, id);
    }

    /// <summary>
    /// Принять награды во всех письмах, где награда ещё доступна
    /// </summary>
    /// <returns>Количество писем, где награда принята</returns>
    /// <response code="200">Награды приняты</response>
    /// <response code="400">Ошибка принятия наград</response>
    /// <response code="401">Пользователь не авторизован</response>
    [ProducesResponseType(typeof(AcceptAllMailsResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [Authorize]
    [HttpPost("accept-all")]
    public async Task<IActionResult> AcceptAllMails()
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await mailStatusUpdatingService.MarkAllRewardMailsAsAcceptedAsync(traderId);
        if (!result.TryGetData(out var acceptedCount))
            return BadRequest(result.Message);

        return Ok(new AcceptAllMailsResponse(acceptedCount));
    }

    private async Task<IActionResult> GetUpdatedMailAsync(long traderId, long mailId)
    {
        var mailResult = await mailQueryService.GetUserMailAsync(traderId, mailId);
        if (!mailResult.TryGetData(out var mail))
            return BadRequest(mailResult.Message);

        return Ok(mail);
    }

    private IActionResult NotFoundMail(string message)
        => message.Contains("не найдено", StringComparison.OrdinalIgnoreCase)
            ? NotFound(message)
            : BadRequest(message);

    private IActionResult ConflictOrNotFound(string message)
    {
        if (message.Contains("не найдено", StringComparison.OrdinalIgnoreCase))
            return NotFound(message);

        return Conflict(message);
    }

    private static bool TryParseFilter(string? filter, out MailFilter mailFilter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            mailFilter = MailFilter.All;
            return true;
        }

        switch (filter.Trim().ToLowerInvariant())
        {
            case "all":
                mailFilter = MailFilter.All;
                return true;
            case "unread":
                mailFilter = MailFilter.Unread;
                return true;
            case "reward":
                mailFilter = MailFilter.Reward;
                return true;
            default:
                mailFilter = MailFilter.All;
                return false;
        }
    }
}