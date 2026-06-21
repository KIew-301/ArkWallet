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

namespace ArkWallet.Presentation.API
{
    [ApiController]
    [Route("api/[controller]")]
    internal class AuthentificationController(ArkWalletDbContext dbContext, ITraderRegistrationService traderRegistrationService, IConfiguration configuration,
        TokenService tokenService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!IsTelegramAuthValid(request.InitData))
                return BadRequest("Некорректные данные авторизации");

            var parts = HttpUtility.ParseQueryString(request.InitData);
            var userJson = parts["user"];

            if (userJson == null)
                return BadRequest("Пустые данные пользователя");

            var user = JsonSerializer.Deserialize<TelegramUser>(userJson);

            if (user == null)
                return BadRequest("Не удалось получить данные пользователя");

            var userId = user.Id;
            var name = user.FirstName;

            if (userId == 0 || name == null || name == "")
                return BadRequest("Некорректные данные пользователя");

            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == userId);

            if (trader == null)
            {
                var registrationResult = await traderRegistrationService.RegisterTraderAsync(userId, name);
                if (!registrationResult.IsSuccess)
                    return BadRequest(registrationResult.ErrorMessage);
            }

            var token = tokenService.GenerateToken(userId);
            return Ok(new LoginResponse(token));
        }

        private bool IsTelegramAuthValid(string initData)
        {
            var parts = HttpUtility.ParseQueryString(initData);
            var hash = parts["hash"];

            if (hash == null)
                return false;

            if (!long.TryParse(parts["auth_date"], out var authDate))
                return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - authDate > 86400)
                return false;

            Dictionary<string, string> dataCheckArr = new();

            foreach (var key in parts.AllKeys)
            {
                if (key != null && key != "hash")
                {
                    dataCheckArr.Add(key, parts[key]);
                }
            }

            var sorted = dataCheckArr.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}");
            string dataCheckString = string.Join("\n", sorted);

            string botToken = configuration["Telegram:BotToken:Main"];

            var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
            var trueCash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));

            var trueCashHex = Convert.ToHexStringLower(trueCash);

            return trueCashHex == hash;
        }

        class TelegramUser
        {
            public long Id { get; set; }
            public string FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Username { get; set; }
        }
    }
}
