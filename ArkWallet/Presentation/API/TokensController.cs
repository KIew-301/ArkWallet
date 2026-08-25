using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер для получения данных о токенах
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[Route("api/v1/tokens")]
public class TokensController(ITokenQueryService tokenQueryService) : ControllerBase
{
    /// <summary>
    /// Получение списка всех активных токенов
    /// </summary>
    /// <returns>Список токенов с основной информацией</returns>
    /// <response code="200">Список токенов успешно получен</response>
    /// <response code="401">Пользователь не авторизован</response>
    /// <response code="400">Ошибка получения данных</response>
    [ProducesResponseType(typeof(GetTokenListResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [Authorize]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var result = await tokenQueryService.GetAllActiveTokensAsync();

        if (!result.TryGetData(out var data))
            return BadRequest(result.Message);

        return Ok(new GetTokenListResponse(data.ToArray()));
    }
}