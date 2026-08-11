using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для управления слотами майнинг-машин
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/[controller]")]
public class MiningSlotsController(
    IMiningMachineSlotQueryService slotQueryService,
    IMiningMachineSlotBuyingService slotBuyingService,
    IMiningMachineSlotSwitchingOrchestrator switchingOrchestrator,
    IMiningMachineSlotTakingTokenOrchestrator takingTokenOrchestrator,
    IMiningMachineSlotSellingOrchestrator sellingOrchestrator) : ControllerBase
{
    private bool TryGetTraderId(out long traderId)
        => long.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out traderId);

    /// <summary>
    /// Получение слотов майнинг-машин текущего пользователя
    /// </summary>
    /// <returns>Список слотов</returns>
    /// <response code="200">Список слотов успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [ProducesResponseType(typeof(GetMiningSlotsResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpGet("slot")]
    public async Task<IActionResult> GetSlots()
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await slotQueryService.TakeSlotsByTraderAsync(traderId);

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetMiningSlotsResponse(data.ToArray()));
    }

    /// <summary>
    /// Покупка майнинг-машины текущим пользователем
    /// </summary>
    /// <param name="request">Идентификатор машины</param>
    /// <returns>Идентификатор созданного слота</returns>
    /// <response code="200">Машина успешно куплена</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка покупки машины</response>
    [ProducesResponseType(typeof(BuyMiningMachineResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpPost("slot")]
    public async Task<IActionResult> BuyMachine([FromBody] BuyMiningMachineRequest request)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await slotBuyingService.BuyMachineAsync(traderId, request.MachineId);

        if (!result.TryGetData(out var slotId))
            return BadRequest(result.Message);

        return Ok(new BuyMiningMachineResponse(slotId));
    }

    /// <summary>
    /// Запуск переключения слота на майнинг другого токена
    /// </summary>
    /// <param name="request">Слот и целевой токен</param>
    /// <returns>Сообщение об успешном запуске переключения</returns>
    /// <response code="200">Переключение успешно запущено</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка переключения</response>
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpPatch("slot/switch-token")]
    public async Task<IActionResult> SwitchTargetToken([FromBody] SwitchMiningTokenRequest request)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await switchingOrchestrator.SwitchTargetTokenAsync(traderId, request.MiningMachineSlotId, request.Symbol);

        if (!result.IsSuccess)
            return BadRequest(result.Message);

        return Ok(new { Message = "Переключение запущено" });
    }

    /// <summary>
    /// Снятие собранных токенов с одной машины текущего пользователя
    /// </summary>
    /// <param name="request">Идентификатор слота машины</param>
    /// <returns>Сообщение об успешном снятии токенов</returns>
    /// <response code="200">Токены успешно собраны</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка снятия токенов</response>
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpPatch("slot/take-tokens")]
    public async Task<IActionResult> TakeTokens([FromBody] TakeMiningTokensRequest request)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await takingTokenOrchestrator.TakeTokensFromMachineAsync(traderId, request.MiningMachineId);

        if (!result.IsSuccess)
            return BadRequest(result.Message);

        return Ok(new { Message = "Токены собраны" });
    }

    /// <summary>
    /// Продажа слота майнинг-машины текущего пользователя
    /// </summary>
    /// <param name="slotId">Идентификатор слота</param>
    /// <returns>Сообщение об успешной продаже</returns>
    /// <response code="200">Машина успешно продана</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка продажи машины</response>
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpDelete("slot/{slotId}")]
    public async Task<IActionResult> SellMachine(long slotId)
    {
        if (!TryGetTraderId(out var traderId))
            return Unauthorized();

        var result = await sellingOrchestrator.SellMachineAsync(traderId, slotId);

        if (!result.IsSuccess)
            return BadRequest(result.Message);

        return Ok(new { Message = "Машина продана" });
    }
}
