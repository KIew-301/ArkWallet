using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Presentation.DTOs
{
    public record GetTradesResponse(TradeItem[] Trades);
    public record TradeItem(string Symbol, string TraderRole, decimal ExecutionPrice, decimal Quantity, decimal Profit, DateTime TradeDateTime);
}
