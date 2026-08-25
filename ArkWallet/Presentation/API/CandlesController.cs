using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения истории цен (свечей) токенов
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/tokens")]
public class CandlesController(ICandleOrchestrator candleOrchestrator) : ControllerBase
{
    /// <summary>
    /// Получение истории цен (свечей) для указанного токена в указанном тайм-фрейме
    /// </summary>
    /// <param name="request">Параметры запроса (символ, период)</param>
    /// <returns>Список свечей</returns>
    /// <response code="200">Список свечей успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [ProducesResponseType(typeof(GetPriceHistoryResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpGet("candle")]
    public async Task<IActionResult> GetPriceCandle([FromQuery] GetPriceHistoryRequest request)
    {
        var (symbol, start, end, tf) = (request.Symbol, request.StartDateTimeOffset, request.EndDateTimeOffset, request.TimeFrameInMinutes);

        var result = await candleOrchestrator.GetAggregatedCandlesAsync(
            symbol, start.UtcDateTime, end.UtcDateTime, tf);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetPriceHistoryResponse(data.ToArray()));
    }
}
