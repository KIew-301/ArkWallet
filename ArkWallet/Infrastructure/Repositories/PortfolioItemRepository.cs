using ArkWallet.Contracts;
using ArkWallet.Data;
using ArkWallet.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Repositories
{
    internal class PortfolioItemRepository : IPortfolioItemRepository
    {
        private readonly ArkWalletDbContext _context;

        public PortfolioItemRepository(ArkWalletDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PortfolioItem?> GetByIdAsync(object id)
        {
            if (id is string portfolioId)
            {
                return await _context.PortfolioItems.FirstOrDefaultAsync(p => p.Id == portfolioId);
            }
            return null;
        }

        public async Task<IEnumerable<PortfolioItem>> GetAllAsync()
        {
            return await _context.PortfolioItems.ToListAsync();
        }

        public async Task AddAsync(PortfolioItem entity)
        {
            await _context.PortfolioItems.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<PortfolioItem> entities)
        {
            await _context.PortfolioItems.AddRangeAsync(entities);
        }

        public async Task UpdateAsync(PortfolioItem entity)
        {
            _context.PortfolioItems.Update(entity);
        }

        public async Task UpdateRangeAsync(IEnumerable<PortfolioItem> entities)
        {
            _context.PortfolioItems.UpdateRange(entities);
        }

        public void RemoveAsync(PortfolioItem entity)
        {
            _context.PortfolioItems.Remove(entity);
        }

        public void RemoveRangeAsync(IEnumerable<PortfolioItem> entities)
        {
            _context.PortfolioItems.RemoveRange(entities);
        }

        public async Task<bool> ExistsAsync(object id)
        {
            if (id is string portfolioId)
            {
                return await _context.PortfolioItems.AnyAsync(p => p.Id == portfolioId);
            }
            return false;
        }

        // Специфичные методы
        public async Task<PortfolioItem?> GetByTraderAndSymbolAsync(long traderId, string symbol)
        {
            symbol = symbol.ToUpper();
            return await _context.PortfolioItems
                .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
        }

        public async Task<List<PortfolioItem>> GetByTraderAsync(long traderId)
        {
            return await _context.PortfolioItems
                .Where(p => p.TraderTelegramId == traderId)
                .ToListAsync();
        }

        public async Task<List<PortfolioItem>> GetByTradersAndSymbolAsync(IEnumerable<long> traderIds, string symbol)
        {
            symbol = symbol.ToUpper();
            return await _context.PortfolioItems
                .Where(p => traderIds.Contains(p.TraderTelegramId) && p.CharacterTokenId == symbol)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalPortfolioValueAsync(long traderId)
        {
            var items = await GetByTraderAsync(traderId);
            return items.Sum(item => item.GetTotalValue());
        }

        public async Task AddOrUpdateAsync(long traderId, string symbol, int quantity, decimal price)
        {
            symbol = symbol.ToUpper();
            var existingItem = await GetByTraderAndSymbolAsync(traderId, symbol);

            if (existingItem == null)
            {
                // Создаем новую запись
                var newItem = new PortfolioItem
                {
                    TraderTelegramId = traderId,
                    CharacterTokenId = symbol,
                    Quantity = quantity,
                    AverageBuyPrice = price,
                    AcquiredAt = DateTime.UtcNow
                };
                await AddAsync(newItem);
            }
            else
            {
                // Обновляем существующую запись с пересчетом средней цены
                var totalQuantity = existingItem.Quantity + quantity;
                var totalValue = (existingItem.Quantity * existingItem.AverageBuyPrice) + (quantity * price);

                existingItem.Quantity = totalQuantity;
                existingItem.AverageBuyPrice = totalValue / totalQuantity;

                _context.PortfolioItems.Update(existingItem);
            }
        }
    }
}