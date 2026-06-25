using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Presentation.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ArkWallet.Presentation.API;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    ITraderRegistrationService traderRegistrationService, 
    IConfiguration configuration, ITokenService tokenService,
    ITraderAuthService traderAuthService) : ControllerBase
{
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
