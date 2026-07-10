using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о сделках трейдера
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TradesController(ITradeQueryService tradeQueryService) : ControllerBase
{
    /// <summary>
    /// Получение истории сделок текущего пользователя
    /// </summary>
    /// <returns>Список сделок с информацией</returns>
    /// <response code="200">История сделок успешно получена</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [HttpGet("trade")]
    [ProducesResponseType(typeof(GetTradesResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetTrades()
    {
        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
            return Unauthorized();

        var result = await tradeQueryService
            .GetTraderTradesAsync(userTelegramId, true);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetTradesResponse(data.ToArray()));
    }
}