using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.SuggestionServices
{
    internal class PriceSuggestionService(ArkWalletDbContext dbContext) : IPriceSuggestionService
    {
        public async Task<List<PriceSuggestionDto>> GetBuyPriceSuggestionsAsync(long traderId, string symbol, int quantity)
        {
            var trader = await dbContext.Traders
                .FirstOrDefaultAsync(t => t.TelegramId == traderId);
            var lastLongOrders = (await dbContext.TradeOrders
                .Where(o => o.Type == OrderType.Buy && o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .AsNoTracking()
                .ToArrayAsync())
                .OrderBy(o => o.Price)
                .ToArray();

            var lastShortOrder = (await dbContext.TradeOrders
                .Where(o => o.Type == OrderType.Sell && o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .AsNoTracking()
                .FirstOrDefaultAsync());

            if (trader == null || lastLongOrders.Length == 0 || lastShortOrder == null)
                return [];

            decimal bid = lastLongOrders[0].Price;
            decimal ask = lastShortOrder.Price;

            decimal maxPrice = Math.Floor(trader.Balance / quantity);
            decimal currentPrice = bid;
            decimal marketPrice = ask;
            decimal goodPrice = lastLongOrders.Average(o => o.Price);
            decimal greatPrice = lastLongOrders[^1].Price;

            List<PriceSuggestionDto> preDto = [];
            List<PriceSuggestionDto> currectDto = [];

            preDto.Add(new(maxPrice,
                "Доступная цена",
                "Максимальная цена, по которой можно купить " +
                $"{quantity} шт. токенов"));

            preDto.Add(new(currentPrice,
                "Истинная цена",
                "Истинная цена токена в данный момент"));

            preDto.Add(new(marketPrice,
                "Рыночная цена",
                "Цена для быстрой покупки"));

            preDto.Add(new(goodPrice,
                "Оптимальная цена",
                "Цена компромиса между выгодой и " +
                "скоростью исполнения"));

            preDto.Add(new(greatPrice,
                "Заниженная цена",
                "Цена для выгодной покупки"));

            preDto = [.. preDto.OrderBy(dto => dto.Price)];

            foreach (var dto in preDto)
            {
                if (dto.Price <= maxPrice)
                    currectDto.Add(dto);
            }

            return currectDto.DistinctBy(d => d.Price).ToList();
        }

        public async Task<List<PriceSuggestionDto>> GetSellPriceSuggestionsAsync(string symbol)
        {
            var activeSells = (await dbContext.TradeOrders
                .Where(o => o.Type == OrderType.Sell && o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .AsNoTracking()
                .ToArrayAsync())
                .OrderBy(o => o.Price)
                .ToArray();

            var bestBid = (await dbContext.TradeOrders
                .Where(o => o.Type == OrderType.Buy && o.CharacterTokenId == symbol && o.Status == OrderStatus.Active)
                .AsNoTracking()
                .ToArrayAsync())
                .OrderByDescending(o => o.Price)
                .FirstOrDefault();

            if (activeSells.Length == 0 || bestBid == null)
                return [];

            decimal bid = bestBid.Price;
            decimal ask = activeSells[0].Price;

            decimal currentPrice = bid;
            decimal marketPrice = ask;
            decimal goodPrice = activeSells.Take(10).Average(o => o.Price);
            decimal greatPrice = activeSells[^1].Price;

            List<PriceSuggestionDto> dto = [];

            dto.Add(new(currentPrice,
                "Истинная цена",
                "Истинная цена токена в данный момент"));

            dto.Add(new(marketPrice,
                "Рыночная цена",
                "Цена для быстрой продажи"));

            dto.Add(new(goodPrice,
                "Оптимальная цена",
                "Цена компромиса между выгодой и " +
                "скоростью исполнения"));

            dto.Add(new(greatPrice,
                "Завышенная цена",
                "Цена для выгодной продажи"));

            dto = [.. dto.OrderByDescending(dto => dto.Price)];

            return dto.DistinctBy(d => d.Price).ToList(); ;
        }
    }
}
