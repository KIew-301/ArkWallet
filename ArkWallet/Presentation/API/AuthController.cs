using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер аутентификации через Telegram WebApp
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    ITraderRegistrationService traderRegistrationService,
    IConfiguration configuration, ITokenService tokenService,
    ITraderAuthService traderAuthService) : ControllerBase
{
    /// <summary>
    /// Вход в систему через Telegram WebApp InitData
    /// </summary>
    /// <param name="request">Данные авторизации из Telegram</param>
    /// <returns>JWT-токен для доступа к API</returns>
    /// <response code="200">Успешная аутентификация, возвращён токен</response>
    /// <response code="401">Неверные данные авторизации</response>
    /// <response code="400">Ошибка регистрации пользователя</response>
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        string? botToken = configuration["Telegram:BotToken:Main"];

        var authResult = traderAuthService.AuthenticateUser(request.InitData, botToken);
        if (!authResult.TryGetData(out var data))
            return Unauthorized();

        var isRegistered = await traderRegistrationService.CheckTraderAlreadyRegistered(data.User.Id);

        if (!isRegistered)
        {
            var registrationResult = await traderRegistrationService
                .RegisterTraderAsync(data.User.Id, data.User.FirstName);
            if (!registrationResult.IsSuccess)
                return BadRequest(registrationResult.Message);
        }

        var token = tokenService.GenerateToken(data.User.Id);
        return Ok(new LoginResponse(token));
    }
}
