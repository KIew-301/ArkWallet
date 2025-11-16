using ArkWallet.Application.Contracts;
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
        }

        public async Task AddRangeAsync(IEnumerable<Trader> entities)
        {
            await _context.Traders.AddRangeAsync(entities);
        }

        public async Task UpdateAsync(Trader entity)
        {
            _context.Traders.Update(entity);
        }

        public async Task UpdateRangeAsync(IEnumerable<Trader> entities)
        {
            _context.Traders.UpdateRange(entities);
        }

        public void RemoveAsync(Trader entity)
        {
            _context.Traders.Remove(entity);
        }

        public void RemoveRangeAsync(IEnumerable<Trader> entities)
        {
            _context.Traders.RemoveRange(entities);
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
            }
        }
    }
}