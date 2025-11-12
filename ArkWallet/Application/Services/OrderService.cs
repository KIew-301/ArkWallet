using ArkWallet.Domain;
using ArkWallet.Entities;
using ArkWallet.Infrastructure;
using ArkWallet.ValueObjects;

namespace ArkWallet.Application.Services
{
    internal class OrderService
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly TradingEngine _tradingEngine;

        public OrderService(UnitOfWork unitOfWork, TradingEngine tradingEngine)
        {
            _unitOfWork = unitOfWork;
            _tradingEngine = tradingEngine;
        }

        public async Task<OrderResult> PlaceOrder(TradeOrder order)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 1. ПОЛУЧАЕМ ДАННЫЕ ДЛЯ TRADINGENGINE
                var existingOrders = await _unitOfWork.GetActiveOrdersForMatchingAsync(order.CharacterTokenId);
                var traders = await _unitOfWork.GetTradersForOrderAsync(order.CharacterTokenId, order.TraderTelegramId);
                var portfolios = await _unitOfWork.GetPortfoliosForTradersAsync(order.CharacterTokenId, traders.Keys);
                var token = await _unitOfWork.GetTokenAsync(order.CharacterTokenId);

                // 2. ВЫЗЫВАЕМ TRADINGENGINE
                var engineResult = _tradingEngine.ProcessOrder(order, [.. existingOrders], traders, portfolios, token);

                if (!engineResult.IsSuccess)
                    return OrderResult.Failed(order, engineResult.Error);

                // 3. СОХРАНЯЕМ РЕЗУЛЬТАТ
                await _unitOfWork.SaveTradingResultAsync(engineResult);

                // 4. ВОЗВРАЩАЕМ РЕЗУЛЬТАТ
                if (engineResult.Trades.Any())
                    return OrderResult.Filled(order, engineResult.Trades);
                else
                    return OrderResult.Pending(order);
            });
        }
    }
}