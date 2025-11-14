using ArkWallet.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    internal interface IPortfolioUpdatingService
    {
        Task<PortfolieUpdatingResult> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity);
    }

    public record PortfolieUpdatingResult(
        bool IsSuccess,
        string? ErrorMessage = null
    );
}
