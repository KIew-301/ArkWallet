using ArkWallet.Application.Contracts.Other;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Infrastructure.Repositories
{
    internal class TraderRepository : ITraderRepository
    {
        private readonly ArkWalletDbContext _context;

        public TraderRepository(ArkWalletDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Trader?> GetByIdAsync(object id)
        {
            if (id is long telegramId)
                return await _context.Traders.FirstOrDefaultAsync(t => t.TelegramId == telegramId);
            return null;
        }

        public async Task<IEnumerable<Trader>> GetAllAsync()
        {
            return await _context.Traders.ToListAsync();
        }

        public async Task AddAsync(Trader entity)
        {
            await _context.Traders.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<Trader> entities)
        {
            await _context.Traders.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Trader entity)
        {
            _context.Traders.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRangeAsync(IEnumerable<Trader> entities)
        {
            _context.Traders.UpdateRange(entities);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(Trader entity)
        {
            _context.Traders.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveRangeAsync(IEnumerable<Trader> entities)
        {
            _context.Traders.RemoveRange(entities);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(object id)
        {
            if (id is long telegramId)
            {
                return await _context.Traders.AnyAsync(t => t.TelegramId == telegramId);
            }
            return false;
        }

        // Специфичные методы
        public async Task<Trader?> GetByTelegramIdAsync(long telegramId)
        {
            return await GetByIdAsync(telegramId);
        }

        public async Task<List<Trader>> GetByIdsAsync(IEnumerable<long> telegramIds)
        {
            return await _context.Traders
                .Where(t => telegramIds.Contains(t.TelegramId))
                .ToListAsync();
        }

        public async Task<bool> ExistsByTelegramIdAsync(long telegramId)
        {
            return await ExistsAsync(telegramId);
        }

        public async Task UpdateBalanceAsync(long telegramId, decimal newBalance)
        {
            var trader = await GetByIdAsync(telegramId);
            if (trader != null)
            {
                trader.Balance = newBalance;
                _context.Traders.Update(trader);
                await _context.SaveChangesAsync();
            }
        }
    }
}