using Microsoft.EntityFrameworkCore;
using ArkWallet.Data;
using ArkWallet.Entities;

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

        public async Task<Trader?> GetAsyncById(long id)
        {
            return await _context.Traders.FirstOrDefaultAsync(t => t.TelegramId == id);
        }

        public async Task AddAsync(Trader trader)
        {
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
            Trader? trader = await GetAsyncById(traderId);

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
            Trader? trader = await GetAsyncById(traderId);

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
            Trader? trader = await GetAsyncById(id);
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
