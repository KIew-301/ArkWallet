using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Services.SuggestionServices
{
    internal class PriceSuggestionService : IPriceSuggestionService
    {
        readonly IUnitOfWork _unitOfWork;

        public PriceSuggestionService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<PriceSuggestionDto>> GetBuyPriceSuggestionsAsync(long traderId, string symbol, int quantity)
        {
            var trader = await _unitOfWork.Traders.GetByIdAsync(traderId);
            var token = await _unitOfWork.Tokens.GetByIdAsync(symbol);

            if (token == null || trader == null) 
                return [];

            var orders = await _unitOfWork.Orders.GetByOptionsAsync(
                traderId,
                symbol,
                OrderType.Buy,
                OrderStatus.Active
            );

            decimal sum = orders.Sum(o => o.Quantity * o.Price);
            decimal stock = trader.Balance - sum;

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
            var item = await _unitOfWork.Portfolios.GetByTraderAndSymbolAsync(traderId, symbol);
            var token = await _unitOfWork.Tokens.GetByIdAsync(symbol);

            var orders = await _unitOfWork.Orders.GetByOptionsAsync(
                traderId,
                symbol,
                OrderType.Sell,
                OrderStatus.Active
            );

            int sum = orders.Sum(o => o.Quantity);
            int stock = item.Quantity - sum;

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