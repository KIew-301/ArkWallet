using Microsoft.EntityFrameworkCore;
using ArkWallet.Data;
using ArkWallet.Entities;

namespace ArkWallet.Repositories
{
    internal class CharacterTokenRepository
    {
        private ArkWalletDbContext _context;

        public CharacterTokenRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<CharacterToken?> GetByIdAsync(string id)
        {
            return await _context.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == id);
        }

        public async Task AddAsync(CharacterToken token)
        {
            var target = GetByIdAsync(token.Symbol);

            if (target != null)
            {
                return;
            }

            await _context.CharacterTokens.AddAsync(token);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(CharacterToken token)
        {
            _context.CharacterTokens.Update(token);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(string id)
        {
            CharacterToken? token = await GetByIdAsync(id);
            if (token == null)
            {
                Console.WriteLine($"Токен {id} не найден.");
            }
            else
            {
                _context.CharacterTokens.Remove(token);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Токен {id} успешно удалён");
            }
        }
    }
}
