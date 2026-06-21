using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkWallet.Domain.Entities
{
    internal class BalanceSnapshot
    {
        public long Id { get; set; }
        public decimal TotalBalance { get; private set; }
        public DateTime SnapshotDateTime { get; private set; }

        public long TraderId { get; set; }
        public Trader? Trader { get; private set; }

        public static BalanceSnapshot Create(decimal totalBalance, long traderId)
        {
            return new BalanceSnapshot
            {
                TotalBalance = totalBalance,
                SnapshotDateTime = DateTime.UtcNow,
                TraderId = traderId
            };
        }
    }
}
