using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.Other
{
    internal class ReserveCalculationService(ArkWalletDbContext dbContext)
    {
        public async Task<int> GetReservedQuantityAsync(long traderId, string symbol)
        {
            return await dbContext.TradeOrders
                .Where(o =>
                    o.TraderTelegramId == traderId &&
                    o.Status == OrderStatus.Active &&
                    o.CharacterTokenId == symbol &&
                    o.Type == OrderType.Sell)
                .SumAsync(o => o.Quantity - o.FilledQuantity);
        }

        public async Task<Dictionary<string, int>> GetReservedQuantitiesAllAsync(long traderId)
        {
            return await dbContext.TradeOrders
                .Where(o =>
                    o.TraderTelegramId == traderId &&
                    o.Status == OrderStatus.Active &&
                    o.Type == OrderType.Sell)
                .GroupBy(o => o.CharacterTokenId)
                .Select(g => new { g.Key, Value = g.Sum(o => o.Quantity - o.FilledQuantity) })
                .ToDictionaryAsync(x => x.Key, x => x.Value);
        }

        public async Task<decimal> GetReservedBalanceAsync(long traderId)
        {
            return await dbContext.TradeOrders
                .Where(o =>
                    o.TraderTelegramId == traderId &&
                    o.Status == OrderStatus.Active &&
                    o.Type == OrderType.Buy)
                .SumAsync(o => (o.Quantity - o.FilledQuantity) * o.Price);
        }

        public async Task<decimal> GetTraderAvailableBalanceAsync(long traderId)
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
            var reserve = await GetReservedBalanceAsync(traderId);
            return trader?.Balance - reserve ?? 0;
        }
    }
}
