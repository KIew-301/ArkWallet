using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о майнинг-машинах
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/[controller]")]
public class MiningMachinesController(IMiningMachineQueryService miningMachineQueryService) : ControllerBase
{
    /// <summary>
    /// Получение списка майнинг-машин, доступных для покупки
    /// </summary>
    /// <returns>Список машин с данными майнинга токенов</returns>
    /// <response code="200">Список машин успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [ProducesResponseType(typeof(GetMiningMachinesResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpGet("machine")]
    public async Task<IActionResult> GetMachines()
    {
        if (!long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var traderId))
            return Unauthorized();

        var result = await miningMachineQueryService.TakeActiveForSaleMachinesAsync(traderId);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetMiningMachinesResponse(data.ToArray()));
    }
}
