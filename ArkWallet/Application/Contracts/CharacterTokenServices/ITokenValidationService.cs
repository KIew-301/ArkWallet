using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Contracts.CharacterTokenServices
{
    interface ITokenValidationService
    {
        Task<ValidationResult> ValidateTokenActivityAsync(long traderId, string symbol, string direction);
    }
}
