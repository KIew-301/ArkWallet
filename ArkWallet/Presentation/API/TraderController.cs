using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о трейдере
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class TradersController(IBalanceChangesCalculationService balanceChangesCalculationService) : ControllerBase
{
    /// <summary>
    /// Получение текущего баланса и его изменений за период
    /// </summary>
    /// <param name="request">Период для расчёта изменений</param>
    /// <returns>Данные о балансе и изменениях</returns>
    /// <response code="200">Данные баланса успешно получены</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [Authorize]
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] GetBalanceRequest request)
    {
        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
            return Unauthorized();

        var result = await balanceChangesCalculationService
            .TakeMainBalanceChanges(userTelegramId, request.PeriodDays);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetBalanceResponse(data.CurrentBalance, data.ChangeAbsolute, data.ChangePercent));
    }
}
