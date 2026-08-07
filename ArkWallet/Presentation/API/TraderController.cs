using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о трейдере
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
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

        var result = await balanceChangesCalculationService
            .TakeBalanceChanges(userTelegramId, request.PeriodDays);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetBalanceResponse(
            new BalanceInfo(data.Main.CurrentBalance, data.Main.ChangeAbsolute, data.Main.ChangePercent),
            new BalanceInfo(data.Total.CurrentBalance, data.Total.ChangeAbsolute, data.Total.ChangePercent)
        ));
    }
}
