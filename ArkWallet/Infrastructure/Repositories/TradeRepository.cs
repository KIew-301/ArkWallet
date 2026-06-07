using ArkWallet.Application.Contracts.Other;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Infrastructure.Repositories
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
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<Trade> entities)
        {
            await _context.Trades.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Trade entity)
        {
            _context.Trades.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRangeAsync(IEnumerable<Trade> entities)
        {
            _context.Trades.UpdateRange(entities);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(Trade entity)
        {
            _context.Trades.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveRangeAsync(IEnumerable<Trade> entities)
        {
            _context.Trades.RemoveRange(entities);
            await _context.SaveChangesAsync();
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