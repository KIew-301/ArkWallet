using ArkWallet.Core.TradingContext.Application.Contracts.TradeServices;
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
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Получение истории сделок текущего пользователя с постраничной выборкой
    /// </summary>
    /// <param name="page">Номер страницы (начиная с 1)</param>
    /// <param name="pageSize">Количество сделок на странице</param>
    /// <returns>Страница сделок с информацией</returns>
    /// <response code="200">Страница сделок успешно получена</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Некорректный номер страницы или размер страницы</response>
    [HttpGet("trade")]
    [ProducesResponseType(typeof(GetTradesPageResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetTrades([FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize)
    {
        if (page < 1) return BadRequest("Номер страницы должен быть больше или равен 1");
        if (pageSize is < 1 or > MaxPageSize) return BadRequest($"Размер страницы должен быть в диапазоне от 1 до {MaxPageSize}");

        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
            return Unauthorized();

        var result = await tradeQueryService
            .GetTraderTradesPageAsync(userTelegramId, page, pageSize, true);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetTradesPageResponse(data.Items.ToArray(), data.Page, data.PageSize, data.TotalCount, data.TotalPages, data.HasNext, data.HasPrevious));
    }
}