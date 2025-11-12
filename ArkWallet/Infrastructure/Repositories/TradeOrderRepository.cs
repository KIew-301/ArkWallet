using ArkWallet.Data;
using ArkWallet.Entities;
using ArkWallet.ValueObjects;
using Microsoft.EntityFrameworkCore;


namespace ArkWallet.Repositories
{
    internal class TradeOrderRepository
    {
        private readonly ArkWalletDbContext _context;

        public TradeOrderRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<TradeOrder?> GetByIdAsync(string id)
        {
            return await _context.TradeOrders.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TradeOrder[]?> GetAllAsync()
        {
            return await _context.TradeOrders.ToArrayAsync();
        }

        public async Task<List<TradeOrder>> GetActiveBySymbolAsync(string symbol)
        {
            return await _context.TradeOrders
                .Where(o => o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .ToListAsync();
        }

        public async Task<TradeOrder[]> GetActiveOrdersByTraderAsync(long traderId)
        {
            return await _context.TradeOrders.Where(t => t.Status == OrderStatus.Active && t.TraderTelegramId == traderId).ToArrayAsync();
        }

        public async Task AddRangeAsync(IEnumerable<TradeOrder> entities)
        {
            await _context.TradeOrders.AddRangeAsync(entities);
        }

        public async Task UpdateRangeAsync(IEnumerable<TradeOrder> entities)
        {
            _context.TradeOrders.UpdateRange(entities);
        }

        public async Task AddAsync(TradeOrder tradeOrder)
        {
            var target = await GetByIdAsync(tradeOrder.Id);

            if (target != null)
            {
                return;
            }

            await _context.TradeOrders.AddAsync(tradeOrder);
        }

        public async Task UpdateAsync(TradeOrder tradeOrder)
        {
            _context.TradeOrders.Update(tradeOrder);
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
                Console.WriteLine($"Ордер {id} успешно удалён");
            }
        }

        public async Task RemoveRange(List<TradeOrder> orders)
        {
            _context.TradeOrders.RemoveRange(orders);
        }
    }
}
