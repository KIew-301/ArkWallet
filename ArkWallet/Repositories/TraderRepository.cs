using ArkWallet.Data;
using ArkWallet.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Repositories
{
    internal class TraderRepository
    {
        private ArkWalletDbContext _context;

        public TraderRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<Trader?> GetByIdAsync(long id)
        {
            return await _context.Traders.FirstOrDefaultAsync(t => t.TelegramId == id);
        }

        public async Task AddAsync(Trader trader)
        {
            var target = await GetByIdAsync(trader.TelegramId);

            if (target != null)
            {
                return;
            }

            await _context.Traders.AddAsync(trader);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Trader trader)
        {
            _context.Traders.Update(trader);
            await _context.SaveChangesAsync();
        }

        public async Task AddBalanceAsync(long traderId, decimal amount)
        {
            Trader? trader = await GetByIdAsync(traderId);

            if (trader != null)
            {
                trader.Balance += amount;
                _context.Traders.Update(trader);
                await _context.SaveChangesAsync();
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
                await _context.SaveChangesAsync();
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
                await _context.SaveChangesAsync();
                Console.WriteLine($"Трейдер {id} успешно удалён");
            }
        }
    }
}
