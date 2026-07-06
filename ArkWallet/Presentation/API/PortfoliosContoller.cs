using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о портфеле трейдера
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class PortfoliosContoller(IPortfolioQueryService portfolioQueryService) : ControllerBase
{
    /// <summary>
    /// Получение текущего портфеля трейдера
    /// </summary>
    /// <returns>Список токенов в портфеле с их количеством</returns>
    /// <response code="200">Данные портфеля успешно получены</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [Authorize]
    [HttpGet("porfolio")]
    public async Task<IActionResult> GetPortfolio()
    {
        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
            return Unauthorized();

        var result = await portfolioQueryService.GetTraderTokensAsync(userTelegramId);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetPortfolioResponse(data));
    }
}