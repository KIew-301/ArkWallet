using Microsoft.EntityFrameworkCore;
using ArkWallet.Data;
using ArkWallet.Entities;

namespace ArkWallet.Repositories
{
    internal class TradeRepository
    {
        private readonly ArkWalletDbContext _context;

        public TradeRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<Trade?> GetByIdAsync(string id)
        {
            return await _context.Trades.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task AddRangeAsync(IEnumerable<Trade> entities)
        {
            await _context.Trades.AddRangeAsync(entities);
        }

        public async Task UpdateRangeAsync(IEnumerable<Trade> entities)
        {
            _context.Trades.UpdateRange(entities);
        }

        public async Task AddAsync(Trade trade)
        {
            var target = await GetByIdAsync(trade.Id);

            if (target != null)
            {
                return;
            }

            await _context.Trades.AddAsync(trade);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Trade trade)
        {
            _context.Trades.Update(trade);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(string id)
        {
            Trade? trade = await GetByIdAsync(id);
            if (trade == null)
            {
                Console.WriteLine($"Обмен {id} не найден.");
            }
            else
            {
                _context.Trades.Remove(trade);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Обмен {id} успешно удалён");
            }
        }

        public async Task RemoveRange(List<Trade> trades)
        {
            _context.Trades.RemoveRange(trades);
        }
    }
}
