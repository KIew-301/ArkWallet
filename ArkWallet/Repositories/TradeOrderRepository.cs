using ArkWallet.Data;
using ArkWallet.Entities;
using ArkWallet.ValueObjects;
using Microsoft.EntityFrameworkCore;


namespace ArkWallet.Repositories
{
    internal class TradeOrderRepository
    {
        private ArkWalletDbContext _context;

        public TradeOrderRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<TradeOrder?> GetByIdAsync(string id)
        {
            return await _context.TradeOrders.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TradeOrder[]> GetActiveOrdersByTraderAsync(long traderId)
        {
            return await _context.TradeOrders.Where(t => t.Status == OrderStatus.Active && t.TraderTelegramId == traderId).ToArrayAsync();
        }

        public async Task AddAsync(TradeOrder tradeOrder)
        {
            var target = GetByIdAsync(tradeOrder.Id);

            if (target != null)
            {
                return;
            }

            await _context.TradeOrders.AddAsync(tradeOrder);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(TradeOrder tradeOrder)
        {
            _context.TradeOrders.Update(tradeOrder);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(string id)
        {
            TradeOrder? tradeOrder = await GetByIdAsync(id);
            if (tradeOrder == null)
            {
                Console.WriteLine($"Ордер {id} не найден.");
            }
            else
            {
                _context.TradeOrders.Remove(tradeOrder);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Ордер {id} успешно удалён");
            }
        }
    }
}
