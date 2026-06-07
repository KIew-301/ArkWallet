using ArkWallet.Application.Contracts.CharacterTokenServices;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Services.CharacterTokenServices
{
    internal class TokenValidationService(ITokenQueryService tokenQueryService) : ITokenValidationService
    {
        public async Task<ValidationResult> ValidateTokenActivityAsync(long traderId, string symbol, string direction)
        {
            var token = await tokenQueryService.GetTokenInfoAsync(symbol);

            if (token == null)
                return new ValidationResult($"Токен с символом '{symbol}' не найден.");
            else
                return new ValidationResult(true);
        }
    }
}
