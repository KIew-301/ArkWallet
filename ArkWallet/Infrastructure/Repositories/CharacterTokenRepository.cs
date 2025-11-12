using ArkWallet.Contracts;
using ArkWallet.Data;
using ArkWallet.Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace ArkWallet.Repositories
{
    internal class CharacterTokenRepository
    {
        private readonly ArkWalletDbContext _context;

        public CharacterTokenRepository(ArkWalletDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            else _context = context;
        }

        public async Task<CharacterToken?> GetByIdAsync(string id)
        {
            id = id.ToUpper();
            return await _context.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == id);
        }

        public async Task AddAsync(CharacterToken token)
        {
            var target = await GetByIdAsync(token.Symbol);

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

        public async Task AddRangeAsync(IEnumerable<CharacterToken> entities)
        {
            await _context.CharacterTokens.AddRangeAsync(entities);
        }

        public async Task UpdateRangeAsync(IEnumerable<CharacterToken> entities)
        {
            _context.CharacterTokens.UpdateRange(entities);
        }

        public async Task RemoveAsync(string id)
        {
            id = id.ToUpper();

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

        public async Task RemoveRange(List<CharacterToken> tokens)
        {
            _context.CharacterTokens.RemoveRange(tokens);
        }
    }
}
