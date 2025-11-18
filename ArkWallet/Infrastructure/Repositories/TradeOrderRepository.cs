using ArkWallet.Application.Contracts;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Infrastructure.Repositories
{
    internal class TradeOrderRepository : ITradeOrderRepository
    {
        private readonly ArkWalletDbContext _context;

        public TradeOrderRepository(ArkWalletDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<TradeOrder?> GetByIdAsync(object id)
        {
            if (id is string orderId)
            {
                return await _context.TradeOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            }
            return null;
        }

        public async Task<IEnumerable<TradeOrder>> GetAllAsync()
        {
            return await _context.TradeOrders.ToListAsync();
        }

        public async Task AddAsync(TradeOrder entity)
        {
            await _context.TradeOrders.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<TradeOrder> entities)
        {
            await _context.TradeOrders.AddRangeAsync(entities);
        }

        public async Task UpdateAsync(TradeOrder entity)
        {
            _context.TradeOrders.Update(entity);
        }

        public async Task UpdateRangeAsync(IEnumerable<TradeOrder> entities)
        {
            _context.TradeOrders.UpdateRange(entities);
        }

        public void RemoveAsync(TradeOrder entity)
        {
            _context.TradeOrders.Remove(entity);
        }

        public void RemoveRangeAsync(IEnumerable<TradeOrder> entities)
        {
            _context.TradeOrders.RemoveRange(entities);
        }

        public async Task<bool> ExistsAsync(object id)
        {
            if (id is string orderId)
            {
                return await _context.TradeOrders.AnyAsync(o => o.Id == orderId);
            }
            return false;
        }

        // Специфичные методы
        public async Task<TradeOrder[]> GetActiveBySymbolAsync(string symbol)
        {
            return await _context.TradeOrders
                .Where(o => o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .ToArrayAsync();
        }

        public async Task<TradeOrder[]> GetByTraderAsync(long traderId)
        {
            return await _context.TradeOrders
                .Where(o => o.TraderTelegramId == traderId)
                .ToArrayAsync();
        }

        public async Task<TradeOrder[]> GetPendingByTraderAsync(long traderId)
        {
            return await _context.TradeOrders
                .Where(o => o.TraderTelegramId == traderId && o.Status == OrderStatus.Active)
                .ToArrayAsync();
        }

        public async Task<bool> CancelOrderAsync(string orderId)
        {
            var order = await GetByIdAsync(orderId);

            if (order == null || order.Status != OrderStatus.Active)
                return false;

            order.Status = OrderStatus.Cancelled;
            _context.TradeOrders.Update(order);

            return true;
        }

        public async Task<TradeOrder[]> GetByOptionsAsync(long traderId, string symbol, OrderType type, OrderStatus status)
        {
            return await _context.TradeOrders
                .Where(o =>
                    o.TraderTelegramId == traderId &&
                    o.CharacterTokenId == symbol &&
                    o.Status == status &&
                    o.Type == type)
                .ToArrayAsync();
        }

        public async Task<int> GetReservedQuantityAsync(long traderId, string symbol)
        {
            return await _context.TradeOrders
                .Where(o =>
                    o.TraderTelegramId == traderId &&
                    o.Status == OrderStatus.Active &&
                    o.CharacterTokenId == symbol &&
                    o.Type == OrderType.Sell)
                .SumAsync(o => o.Quantity - o.FilledQuantity);
        }

        public async Task<Dictionary<string, int>> GetReservedQuantitiesAllAsync(long traderId)
        {
            return await _context.TradeOrders
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
            return await _context.TradeOrders
                .Where(o =>
                    o.TraderTelegramId == traderId &&
                    o.Status == OrderStatus.Active &&
                    o.Type == OrderType.Buy)
                .SumAsync(o => (o.Quantity - o.FilledQuantity) * o.Price);
        }
    }
}