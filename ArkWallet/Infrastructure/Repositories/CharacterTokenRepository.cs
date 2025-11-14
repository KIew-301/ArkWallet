using ArkWallet.Application.Contracts;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace ArkWallet.Infrastructure.Repositories
{
    internal class CharacterTokenRepository : ICharacterTokenRepository
    {
        private readonly ArkWalletDbContext _context;

        public CharacterTokenRepository(ArkWalletDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<CharacterToken?> GetByIdAsync(object id)
        {
            if (id is string symbol)
            {
                symbol = symbol.ToUpper();
                return await _context.CharacterTokens.FirstOrDefaultAsync(t => t.Symbol == symbol);
            }

            return null;
        }

        public async Task<CharacterToken?> GetBySymbolAsync(string symbol)
        {
            return await GetByIdAsync(symbol);
        }

        public async Task<IEnumerable<CharacterToken>> GetAllAsync()
        {
            return await _context.CharacterTokens.ToListAsync();
        }

        public async Task AddAsync(CharacterToken token)
        {
            var target = await GetByIdAsync(token.Symbol);
            await _context.CharacterTokens.AddAsync(token);
        }

        public async Task UpdateAsync(CharacterToken token)
        {
            _context.CharacterTokens.Update(token);
        }

        public async Task AddRangeAsync(IEnumerable<CharacterToken> entities)
        {
            await _context.CharacterTokens.AddRangeAsync(entities);
        }

        public async Task UpdateRangeAsync(IEnumerable<CharacterToken> entities)
        {
            _context.CharacterTokens.UpdateRange(entities);
        }
        public void RemoveAsync(CharacterToken entity)
        {
            _context.CharacterTokens.Remove(entity);
        }

        public void RemoveRangeAsync(IEnumerable<CharacterToken> entities)
        {
            _context.CharacterTokens.RemoveRange(entities);
        }

        public async Task<bool> ExistsAsync(object id)
        {
            return await GetByIdAsync(id) != null;
        }

        public async Task<List<CharacterToken>> GetActiveTokensAsync()
        {
            return await _context.CharacterTokens
                .Where(t => t.IsActive)
                .ToListAsync();
        }

        public async Task<List<CharacterToken>> GetByRarityAsync(CharacterRarity rarity)
        {
            return await _context.CharacterTokens
                .Where(t => t.Rarity == rarity)
                .ToListAsync();
        }
    }
}
