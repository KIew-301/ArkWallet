using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record GetTokenListResponse(TokenItem[] Tokens);
    public record TokenItem(string Symbol, string TokenName, decimal Price, decimal DailyChangePercent, string IconUrl, string ImageUrl);
}
