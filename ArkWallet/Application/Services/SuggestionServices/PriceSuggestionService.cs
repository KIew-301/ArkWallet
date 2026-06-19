using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.SuggestionServices
{
    internal class PriceSuggestionService(ArkWalletDbContext dbContext, ReserveCalculationService reserveCalculationService) : IPriceSuggestionService
    {
        public async Task<List<PriceSuggestionDto>> GetBuyPriceSuggestionsAsync(long traderId, string symbol, int quantity)
        {
            var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.TelegramId == traderId);
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(c => c.Symbol == symbol);

            if (token == null || trader == null)
                return [];

            decimal stock = trader.Balance;

            decimal optimalPrice = Math.Floor(stock / quantity * 100) / 100;
            decimal currentPrice = token.CurrentPrice;
            decimal noBestPrice = token.CurrentPrice * 1.05M;
            decimal closeBestPrice = token.CurrentPrice * 0.95M;
            decimal farBestPrice = token.CurrentPrice * 0.80M;

            List<PriceSuggestionDto> preDto = [];
            List<PriceSuggestionDto> currectDto = [];

            preDto.Add(new(optimalPrice,
                "Доступная цена",
                "Максимальная цена, по которой можно купить" +
                $"{quantity} шт. токенов"));

            preDto.Add(new(currentPrice,
                "Истинная цена",
                "Истинная цена токена в данный момент"));

            preDto.Add(new(noBestPrice,
                "Рыночная цена",
                "Цена для быстрой покупки"));

            preDto.Add(new(closeBestPrice,
                "Оптимальная цена",
                "Цена компромиса между выгодой и " +
                "скоростью исполнения"));

            preDto.Add(new(farBestPrice,
                "Заниженная цена",
                "Цена для выгодной покупки"));

            preDto = [.. preDto.OrderBy(dto => dto.Price)];

            foreach (var dto in preDto)
            {
                if (dto.Price <= optimalPrice && dto.Price < currentPrice * 1.30M)
                    currectDto.Add(dto);
            }

            return currectDto;
        }

        public async Task<List<PriceSuggestionDto>> GetSellPriceSuggestionsAsync(long traderId, string symbol, int quantity)
        {
            var item = await dbContext.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
            var token = await dbContext.CharacterTokens.FirstOrDefaultAsync(c => c.Symbol == symbol);

            decimal currentPrice = token.CurrentPrice;
            decimal noBestPrice = token.CurrentPrice * 0.95M;
            decimal closeBestPrice = token.CurrentPrice * 1.05M;
            decimal farBestPrice = token.CurrentPrice * 1.20M;

            List<PriceSuggestionDto> dto = [];

            dto.Add(new(currentPrice,
                "Истинная цена",
                "Истинная цена токена в данный момент"));

            dto.Add(new(noBestPrice,
                "Рыночная цена",
                "Цена для быстрой продажи"));

            dto.Add(new(closeBestPrice,
                "Оптимальная цена",
                "Цена компромиса между выгодой и " +
                "скоростью исполнения"));

            dto.Add(new(farBestPrice,
                "Заниженная цена",
                "Цена для выгодной продажи"));

            dto = [.. dto.OrderByDescending(dto => dto.Price)];

            return dto;
        }
    }
}