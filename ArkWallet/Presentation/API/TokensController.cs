using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о токенах
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class TokensController(ITokenQueryService tokenQueryService, ITokenPriceCandleQueryService tokenPriceCandleQueryService) : ControllerBase
{
    /// <summary>
    /// Получение списка всех активных токенов
    /// </summary>
    /// <returns>Список токенов с основной информацией</returns>
    /// <response code="200">Список токенов успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [Authorize]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var result = await tokenQueryService.GetAllActiveTokensAsync();

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetTokenListResponse(data.ToArray()));
    }

    /// <summary>
    /// Получение истории цен (свечей) для указанного токена
    /// </summary>
    /// <param name="request">Параметры запроса (символ, период)</param>
    /// <returns>Список свечей</returns>
    /// <response code="200">Список свечей успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [Authorize]
    [HttpGet("candle")]
    public async Task<IActionResult> GetPriceCandle([FromQuery] GetPriceHistoryRequest request)
    {
        var (symbol, start, end) = (request.Symbol, request.StartDateTimeOffset, request.EndDateTimeOffset);

        var result = await tokenPriceCandleQueryService.GetPriceCandlesAsync(
            symbol, start.UtcDateTime, end.UtcDateTime);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetPriceHistoryResponse(data.ToArray()));
    }
}