using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

[ApiController]
public class PortfoliosContoller(IPortfolioQueryService portfolioQueryService) : ControllerBase
{
    [Authorize]
    [HttpGet("porfolio")]
    public async Task<IActionResult> GetPortfolio()
    {
        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
            return Unauthorized();

        var result = await portfolioQueryService.GetTraderTokensAsync(userTelegramId);

        if (result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetPortfolioResponse(data));
    }
}