using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.SuggestionServices
{
    internal class QuantitySuggestionService(ArkWalletDbContext dbContext) : IQuantitySuggestionService
    {
        private static readonly decimal[] Percentages = [1.0m, 0.5m, 0.25m, 0.10m, 0.05m];

        public async Task<List<QuantitySuggestionDto>> GetBuyQuantitySuggestionsAsync(long traderId, string symbol)
        {
            var trader = await dbContext.Traders
                .FirstOrDefaultAsync(t => t.TelegramId == traderId);

            if (trader == null)
                return [];

            var token = await dbContext.CharacterTokens
                .FirstOrDefaultAsync(t => t.Symbol == symbol);

            if (token == null || token.CurrentPrice <= 0)
                return [];

            var suggestions = Percentages
                .Select(p => (int)Math.Floor(trader.Balance * p / token.CurrentPrice))
                .Where(q => q > 0)
                .Distinct()
                .OrderByDescending(q => q)
                .Select(q => new QuantitySuggestionDto(q))
                .ToList();

            return suggestions;
        }

        public async Task<List<QuantitySuggestionDto>> GetSellQuantitySuggestionsAsync(long traderId, string symbol)
        {
            var portfolioItem = await dbContext.PortfolioItems
                .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);

            if (portfolioItem == null || portfolioItem.Quantity <= 0)
                return [];

            var totalQuantity = portfolioItem.Quantity;

            var suggestions = Percentages
                .Select(p => (int)Math.Floor(totalQuantity * p))
                .Where(q => q > 0)
                .Distinct()
                .OrderByDescending(q => q)
                .Select(q => new QuantitySuggestionDto(q))
                .ToList();

            return suggestions;
        }
    }
}
