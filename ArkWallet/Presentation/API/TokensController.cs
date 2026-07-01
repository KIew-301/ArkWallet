using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkWallet.Presentation.API;

[ApiController]
[Route("api/v1/[controller]")]
public class TokensController(ITokenQueryService tokenQueryService, ITokenPriceCandleQueryService tokenPriceCandleQueryService) : ControllerBase
{
    [Authorize]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var result = await tokenQueryService.GetAllActiveTokensAsync();

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetTokenListResponse(data.ToArray()));
    }

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