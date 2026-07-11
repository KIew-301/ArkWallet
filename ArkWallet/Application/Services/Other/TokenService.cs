using ArkWallet.Application.Contracts.Other;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ArkWallet.Application.Services.Other;

[ExcludeFromCodeCoverage(Justification = "JWT-инфраструктура, зависит от IConfiguration, нет бизнес-логики.")]
internal class TokenService(IConfiguration configuration) : ITokenService
{
    private readonly string _jwtKey = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured in appsettings");

    public string GenerateToken(long userTelegramId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userTelegramId.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
