using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Presentation.API;

/// <summary>
/// Контроллер аутентификации через Telegram WebApp
/// </summary>
[ExcludeFromCodeCoverage(Justification = "API-контроллер: только маршрутизация HTTP-запросов к сервисам. Не содержит бизнес-логики, тестируется интеграционно.")]
[ApiController]
[EnableRateLimiting("auth")]
[Route("api/v1/[controller]")]
public class AuthController(
    ITraderRegistrationService traderRegistrationService,
    IConfiguration configuration, ITokenService tokenService,
    ITraderAuthService traderAuthService,
    AccessControlService accessControl,
    ILogger<AuthController> logger) : ControllerBase
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
        {
            logger.LogWarning("Auth failed: {Reason}", authResult.Message);
            return Unauthorized();
        }

        logger.LogInformation("Auth success for user {UserId} ({FirstName})", data.User.Id, data.User.FirstName);

        if (!accessControl.IsAuthorized(data.User.Id))
            return StatusCode(403, "Access denied");

        var isRegistered = await traderRegistrationService.CheckTraderAlreadyRegistered(data.User.Id);

        if (!isRegistered)
        {
            var registrationResult = await traderRegistrationService
                .RegisterTraderAsync(data.User.Id, data.User.FirstName);
            if (!registrationResult.IsSuccess)
            {
                logger.LogWarning("Registration failed for user {UserId} ({FirstName}): {Reason}", data.User.Id, data.User.FirstName, registrationResult.Message);
                return BadRequest(registrationResult.Message);
            }

            logger.LogInformation("Auto-registered new user {UserId} ({FirstName})", data.User.Id, data.User.FirstName);
        }

        var token = tokenService.GenerateToken(data.User.Id);
        return Ok(new LoginResponse(token));
    }
}
