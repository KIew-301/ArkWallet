using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Services.TradeOrderServices
{
    internal class OrderCreationService : IOrderCreationService
    {
        readonly IUnitOfWork _unitOfWork;
        readonly TradingEngine _tradingEngine;

        public OrderCreationService(
            IUnitOfWork unitOfWork, 
            TradingEngine tradingEngine
            )
        {
            _unitOfWork = unitOfWork;
            _tradingEngine = tradingEngine;
        }

        public async Task<OrderCreationResult> CreateOrderAsync(CreateOrderCommand command)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                try
                {
                    var trader = await _unitOfWork.Traders.GetByIdAsync(command.TraderId);
                    var token = await _unitOfWork.Tokens.GetBySymbolAsync(command.Symbol);

                    if (trader == null)
                        return new OrderCreationResult(false, false, null, "Пользователя не существует");

                    if (token == null)
                        return new OrderCreationResult(false, false, null, "Токена не существует");

                    var orderType = command.Direction.Equals("купить", StringComparison.CurrentCultureIgnoreCase)
                        ? OrderType.Buy
                        : OrderType.Sell;

                    var order = TradeOrder.Create(
                        orderType,
                        command.Symbol,
                        command.TraderId,
                        command.Price,
                        command.Quantity
                    );

                    var existingOrders = await GetActiveOrdersForMatchingAsync(order.CharacterTokenId);
                    var traders = await GetTradersForOrderAsync(order.CharacterTokenId, order.TraderTelegramId);
                    var portfolios = await GetPortfoliosForTradersAsync(order.CharacterTokenId, traders.Keys);

                    var engineResult = _tradingEngine.ProcessOrder(order, existingOrders.ToList(), traders, portfolios, token);

                    if (!engineResult.IsSuccess)
                        return new OrderCreationResult(false, false, null, "Не удалось выставить ордер");

                    await SaveTradingResultAsync(engineResult);

                    string status = order.IsFilled() ? "Исполнен" : "Активен";

                    var result = OrderDto.FromEntity(order);

                    return new OrderCreationResult(true, order.IsFilled(), result);
                }
                catch (DomainException ex)
                {
                    return new OrderCreationResult(false, false, null, ex.Message);
                }
                catch (Exception ex)
                {
                    return new OrderCreationResult(false, false, null, "Ошибка системы");
                }
            });
        }

        private async Task<TradeOrder[]> GetActiveOrdersForMatchingAsync(string symbol)
        {
            return await _unitOfWork.Orders.GetActiveBySymbolAsync(symbol);
        }

        private async Task<Dictionary<long, Trader>> GetTradersForOrderAsync(string symbol, long newOrderTraderId)
        {
            var activeOrders = await _unitOfWork.Orders.GetActiveBySymbolAsync(symbol);
            var traderIds = activeOrders
                .Select(o => o.TraderTelegramId)
                .Append(newOrderTraderId)
                .Distinct()
                .ToHashSet();

            var traders = await _unitOfWork.Traders.GetByIdsAsync(traderIds);
            return traders.ToDictionary(t => t.TelegramId);
        }

        private async Task<Dictionary<long, PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
        {
            var portfolios = await _unitOfWork.Portfolios.GetByTradersAndSymbolAsync(traderIds, symbol);
            return portfolios.ToDictionary(p => p.TraderTelegramId);
        }

        private async Task SaveTradingResultAsync(TradingResult result)
        {
            if (result.Trades.Any())
                await _unitOfWork.Trades.AddRangeAsync(result.Trades);

            if (result.UpdatedOrders.Any())
                await _unitOfWork.Orders.UpdateRangeAsync(result.UpdatedOrders);

            if (result.UpdatedTraders.Any())
                await _unitOfWork.Traders.UpdateRangeAsync(result.UpdatedTraders);

            if (result.UpdatedPortfolios.Any())
                await _unitOfWork.Portfolios.UpdateRangeAsync(result.UpdatedPortfolios);

            if (result.UpdatedToken != null)
                await _unitOfWork.Tokens.UpdateAsync(result.UpdatedToken);

            if (result.OrderToAdd != null)
                await _unitOfWork.Orders.AddAsync(result.OrderToAdd);
        }
    }
}
