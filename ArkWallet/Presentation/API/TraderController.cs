using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    [ProducesResponseType(typeof(GetBalanceResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] GetBalanceRequest request)
    {
        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userTelegramId))
            return Unauthorized();

        var resultMain = await balanceChangesCalculationService
            .TakeMainBalanceChanges(userTelegramId, request.PeriodDays);

        var resultTotal = await balanceChangesCalculationService
            .TakeTotalBalanceChanges(userTelegramId, request.PeriodDays);

        if (!resultMain.TryGetData(out var dataMain))
            return BadRequest(resultMain.Message);

        if (!resultTotal.TryGetData(out var dataTotal))
            return BadRequest(resultTotal.Message);

        return Ok(new GetBalanceResponse(
            new BalanceInfo(dataMain.CurrentBalance, dataMain.ChangeAbsolute, dataMain.ChangePercent),
            new BalanceInfo(dataTotal.CurrentBalance, dataTotal.ChangeAbsolute, dataTotal.ChangePercent)
        ));
    }
}
