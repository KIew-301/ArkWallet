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
        public decimal MainBalance { get; private set; }
        public decimal LongOrderReserveBalance { get; private set; }
        public decimal ShortOrderReserveBalance { get; private set; }
        public decimal BalanceInTokens { get; private set; }
        public DateTime SnapshotDateTime { get; private set; }

        public long TraderId { get; set; }
        public Trader? Trader { get; private set; }

        public static BalanceSnapshot Create(
            long traderId, decimal totalBalance, decimal mainBalance, 
            decimal longOrderReserveBalance, decimal shortOrderReserveBalance, 
            decimal balanceInTokens, DateTime snapshotDateTime)
        {
            return new BalanceSnapshot
            {
                TraderId = traderId,
                TotalBalance = totalBalance,
                MainBalance = mainBalance,
                LongOrderReserveBalance = longOrderReserveBalance,
                ShortOrderReserveBalance = shortOrderReserveBalance,
                BalanceInTokens = balanceInTokens,
                SnapshotDateTime = snapshotDateTime
            };
        }
    }
}
