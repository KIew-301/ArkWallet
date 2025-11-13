using ArkWallet.Application.Contracts;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Repositories
{
    internal class TradeRepository : ITradeRepository
    {
        private readonly ArkWalletDbContext _context;

        public TradeRepository(ArkWalletDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Trade?> GetByIdAsync(object id)
        {
            if (id is string tradeId)
            {
                return await _context.Trades.FirstOrDefaultAsync(t => t.Id == tradeId);
            }
            return null;
        }

        public async Task<IEnumerable<Trade>> GetAllAsync()
        {
            return await _context.Trades.ToListAsync();
        }

        public async Task AddAsync(Trade entity)
        {
            await _context.Trades.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<Trade> entities)
        {
            await _context.Trades.AddRangeAsync(entities);
        }

        public async Task UpdateAsync(Trade entity)
        {
            _context.Trades.Update(entity);
        }

        public async Task UpdateRangeAsync(IEnumerable<Trade> entities)
        {
            _context.Trades.UpdateRange(entities);
        }

        public void RemoveAsync(Trade entity)
        {
            _context.Trades.Remove(entity);
        }

        public void RemoveRangeAsync(IEnumerable<Trade> entities)
        {
            _context.Trades.RemoveRange(entities);
        }

        public async Task<bool> ExistsAsync(object id)
        {
            if (id is string tradeId)
            {
                return await _context.Trades.AnyAsync(t => t.Id == tradeId);
            }
            return false;
        }

        // Специфичные методы
        public async Task<Trade[]> GetByTraderAsync(long traderId)
        {
            return await _context.Trades
                .Where(t => t.BuyerId == traderId || t.SellerId == traderId)
                .ToArrayAsync();
        }

        public async Task<Trade[]> GetBySymbolAsync(string symbol)
        {
            return await _context.Trades
                .Where(t => t.CharacterTokenId == symbol)
                .ToArrayAsync();
        }

        public async Task<Trade[]> GetRecentTradesAsync(int count)
        {
            return await _context.Trades
                .OrderByDescending(t => t.ExecutedAt)
                .Take(count)
                .ToArrayAsync();
        }
    }
}