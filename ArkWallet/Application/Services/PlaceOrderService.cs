using ArkWallet.Application.Contracts;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Services;

namespace ArkWallet.Application.Services
{
    internal class PlaceOrderService
    {
        private readonly IUnitOfWork _uow;
        private readonly TradingEngine _tradingEngine;

        public PlaceOrderService(IUnitOfWork uow, TradingEngine tradingEngine)
        {
            _uow = uow;
            _tradingEngine = tradingEngine;
        }

        public async Task<OrderResult> PlaceOrder(TradeOrder order)
        {
            return await _uow.ExecuteInTransactionAsync(async () =>
            {
                // 1. ПОЛУЧАЕМ ДАННЫЕ ДЛЯ TRADINGENGINE (логика в OrderService)
                var existingOrders = await GetActiveOrdersForMatchingAsync(order.CharacterTokenId);
                var traders = await GetTradersForOrderAsync(order.CharacterTokenId, order.TraderTelegramId);
                var portfolios = await GetPortfoliosForTradersAsync(order.CharacterTokenId, traders.Keys);
                var token = await GetTokenAsync(order.CharacterTokenId);

                // 2. ВЫЗЫВАЕМ TRADINGENGINE
                var engineResult = _tradingEngine.ProcessOrder(order, existingOrders.ToList(), traders, portfolios, token);

                if (!engineResult.IsSuccess)
                    return OrderResult.Failed(order, engineResult.Error);

                // 3. СОХРАНЯЕМ РЕЗУЛЬТАТ (логика в OrderService)
                await SaveTradingResultAsync(engineResult);

                // 4. ВОЗВРАЩАЕМ РЕЗУЛЬТАТ
                return engineResult.Trades.Any()
                    ? OrderResult.Filled(order, engineResult.Trades)
                    : OrderResult.Pending(order);
            });
        }

        // 🔥 ЛОГИКА ПЕРЕНЕСЕНА ИЗ UnitOfWork В OrderService

        private async Task<TradeOrder[]> GetActiveOrdersForMatchingAsync(string symbol)
        {
            return await _uow.Orders.GetActiveBySymbolAsync(symbol);
        }

        private async Task<Dictionary<long, Trader>> GetTradersForOrderAsync(string symbol, long newOrderTraderId)
        {
            var activeOrders = await _uow.Orders.GetActiveBySymbolAsync(symbol);
            var traderIds = activeOrders
                .Select(o => o.TraderTelegramId)
                .Append(newOrderTraderId)
                .Distinct()
                .ToHashSet();

            var traders = await _uow.Traders.GetByIdsAsync(traderIds);
            return traders.ToDictionary(t => t.TelegramId);
        }

        private async Task<Dictionary<long, PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
        {
            var portfolios = await _uow.Portfolios.GetByTradersAndSymbolAsync(traderIds, symbol);
            return portfolios.ToDictionary(p => p.TraderTelegramId);
        }

        private async Task<CharacterToken> GetTokenAsync(string symbol)
        {
            return await _uow.Tokens.GetBySymbolAsync(symbol)
                ?? throw new Exception($"Токен {symbol} не найден");
        }

        private async Task SaveTradingResultAsync(TradingResult result)
        {
            if (result.Trades.Any())
                await _uow.Trades.AddRangeAsync(result.Trades);

            if (result.UpdatedOrders.Any())
                await _uow.Orders.UpdateRangeAsync(result.UpdatedOrders);

            if (result.UpdatedTraders.Any())
                await _uow.Traders.UpdateRangeAsync(result.UpdatedTraders);

            if (result.UpdatedPortfolios.Any())
                await _uow.Portfolios.UpdateRangeAsync(result.UpdatedPortfolios);

            if (result.UpdatedToken != null)
                await _uow.Tokens.UpdateAsync(result.UpdatedToken);

            if (result.OrderToAdd != null)
                await _uow.Orders.AddAsync(result.OrderToAdd);
        }
    }
}