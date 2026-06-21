using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record GetBalanceRequest(int PeriodDays);
    public record GetBalanceResponse(decimal CurrentBalance, decimal ChangeAbsolute, decimal ChangePercent);
}
