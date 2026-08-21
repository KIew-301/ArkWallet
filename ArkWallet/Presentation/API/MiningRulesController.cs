using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных майнинга токенов
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/tokens")]
public class MiningRulesController(IMiningGlobalRuleQueryService miningGlobalRuleQueryService) : ControllerBase
{
    /// <summary>
    /// Получение данных майнинга токенов (глобальные правила и статусы прибыльности)
    /// </summary>
    /// <returns>Список правил майнинга токенов</returns>
    /// <response code="200">Список правил успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [ProducesResponseType(typeof(GetMiningRulesResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpGet("mining-rule")]
    public async Task<IActionResult> GetMiningRules()
    {
        var result = await miningGlobalRuleQueryService.TakeRulesAsync();

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetMiningRulesResponse(data.ToArray()));
    }
}
