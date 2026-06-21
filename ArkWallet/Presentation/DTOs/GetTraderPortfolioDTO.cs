using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record GetPortfolioResponse(PortfolioItem[] Items);
    public record PortfolioItem(string Symbol, string TokenName, decimal Quantity, decimal BalanceInToken, decimal ProfitPercent);
}
