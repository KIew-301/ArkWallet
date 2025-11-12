using ArkWallet.Data;
using ArkWallet.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Repositories
{
    internal class TraderRepository
    {
        private readonly ArkWalletDbContext _context;

        public TraderRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<Trader?> GetByIdAsync(long id)
        {
            return await _context.Traders.FirstOrDefaultAsync(t => t.TelegramId == id);
        }

        public async Task<List<Trader>> GetByIdsAsync(IEnumerable<long> telegramIds)
        {
            return await _context.Traders
                .Where(t => telegramIds.Contains(t.TelegramId))
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<Trader> entities)
        {
            await _context.Traders.AddRangeAsync(entities);
        }

        public async Task UpdateRangeAsync(IEnumerable<Trader> entities)
        {
            _context.Traders.UpdateRange(entities);
        }

        public async Task AddAsync(Trader trader)
        {
            var target = await GetByIdAsync(trader.TelegramId);

            if (target != null)
            {
                return;
            }

            await _context.Traders.AddAsync(trader);
        }

        public async Task UpdateAsync(Trader trader)
        {
            _context.Traders.Update(trader);
        }

        public async Task AddBalanceAsync(long traderId, decimal amount)
        {
            Trader? trader = await GetByIdAsync(traderId);

            if (trader != null)
            {
                trader.Balance += amount;
                _context.Traders.Update(trader);
            }
            else
            {
                Console.WriteLine($"Трейдер {traderId} не найден.");
            }
        }

        public async Task DeductBalanceAsync(long traderId, decimal amount)
        {
            Trader? trader = await GetByIdAsync(traderId);

            if (trader != null)
            {
                trader.Balance -= amount;
                _context.Traders.Update(trader);
            }
            else
            {
                Console.WriteLine($"Трейдер {traderId} не найден.");
            }
        }

        public async Task RemoveAsync(long id)
        {
            Trader? trader = await GetByIdAsync(id);
            if (trader == null)
            {
                Console.WriteLine($"Трейдер {id} не найден.");
            }
            else
            {
                _context.Traders.Remove(trader);
                Console.WriteLine($"Трейдер {id} успешно удалён");
            }
        }

        public async Task RemoveRangeAsync(List<Trader> traders)
        {
            _context.Traders.RemoveRange(traders);
        }
    }
}
