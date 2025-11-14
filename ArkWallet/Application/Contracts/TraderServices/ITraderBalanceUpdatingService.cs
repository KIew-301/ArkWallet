using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Contracts.TraderServices
{
    internal interface ITraderBalanceUpdatingService
    {
        Task<TokenCreationResult> AddToBalanceAsync(long traderId, decimal amount);
    }

    public record TraderBalanceUpdatingResult(
        bool IsSuccess,
        string? ErrorMessage = null
    );
}
