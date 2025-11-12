using ArkWallet.Data;
using ArkWallet.Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace ArkWallet.Repositories
{
    internal class PortfolioItemRepository
    {
        private ArkWalletDbContext _context;

        public PortfolioItemRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<PortfolioItem?> GetByIdAsync(string id)
        {
            return await _context.PortfolioItems.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PortfolioItem[]> GetAllByTraderAsync(long traderId)
        {
            return await _context.PortfolioItems.Where(p => p.TraderTelegramId == traderId).ToArrayAsync();
        }

        public async Task<PortfolioItem?> GetBySymbolAndOwnerAsync(long ownerId, string symbol)
        {
            symbol = symbol.ToUpper();
            return await _context.PortfolioItems.FirstOrDefaultAsync(p => p.CharacterTokenId == symbol && ownerId == p.TraderTelegramId);
        }

        public async Task AddAsync(PortfolioItem item)
        {
            var target = await GetByIdAsync(item.Id);

            if (target != null)
            {
                return;
            }

            await _context.PortfolioItems.AddAsync(item);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(PortfolioItem item)
        {
            _context.PortfolioItems.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task AddOrUpdateAsync(long ownerId, string symbol, int quantity, decimal price)
        {
            symbol = symbol.ToUpper();
            PortfolioItem? item = await GetBySymbolAndOwnerAsync(ownerId, symbol);

            if (item == null)
            {
                var newItem = new PortfolioItem()
                {
                    TraderTelegramId = ownerId,
                    CharacterTokenId = symbol,
                    Quantity = quantity,
                    AverageBuyPrice = price,
                };

                await _context.PortfolioItems.AddAsync(newItem);
                await _context.SaveChangesAsync();
            }
            else
            {
                item.Quantity += quantity;

                item.AverageBuyPrice = 
                    (item.AverageBuyPrice * (item.Quantity - quantity) + price * quantity)
                             / item.Quantity;

                _context.PortfolioItems.Update(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveOrUpdateAsync(long ownerId, string symbol, int quantity)
        {
            PortfolioItem? item = await GetBySymbolAndOwnerAsync(ownerId, symbol);

            if (item != null)
            {
                item.Quantity -= quantity;

                if (item.Quantity > 0)
                {
                    _context.PortfolioItems.Update(item);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    await RemoveAsync(item);
                }
            }
        }

        public async Task RemoveAsyncById(string id)
        {
            PortfolioItem? item = await GetByIdAsync(id);
            if (item == null)
            {
                Console.WriteLine($"Токен {id} у пользователя не найден.");
            }
            else
            {
                _context.PortfolioItems.Remove(item);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Токен {id} у пользователя успешно удалён");
            }
        }

        public async Task RemoveAsync(PortfolioItem item)
        {
            _context.PortfolioItems.Remove(item);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Токен {item.CharacterTokenId} у пользователя успешно удалён");
        }
    }
}
