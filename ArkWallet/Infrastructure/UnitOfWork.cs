using ArkWallet.Data;
using ArkWallet.Entities;
using ArkWallet.Repositories;
using ArkWallet.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArkWallet.Infrastructure
{
    internal class UnitOfWork
    {
        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;
        private readonly TradeOrderRepository _orderRepo;
        private readonly TradeRepository _tradeRepo;
        private readonly ArkWalletDbContext _dbContext;

        public UnitOfWork(TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo,
            TradeOrderRepository orderRepo,
            TradeRepository tradeRepository,
            ArkWalletDbContext dbContext)
        {
            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
            _orderRepo = orderRepo;
            _tradeRepo = tradeRepository;
            _dbContext = dbContext;
        }

        public async Task<TradeOrder[]> GetActiveOrdersForMatchingAsync(string symbol)
        {
            return await _orderRepo.GetActiveBySymbolAsync(symbol);
        }

        public async Task<Dictionary<long, Trader>> GetTradersForOrderAsync(string symbol, long newOrderTraderId)
        {
            // 1. Получаем активные ордера по символу
            var activeOrders = await _orderRepo.GetActiveBySymbolAsync(symbol);

            // 2. Собираем ID всех трейдеров
            var traderIds = activeOrders
                .Select(o => o.TraderTelegramId)
                .Append(newOrderTraderId)
                .Distinct()
                .ToHashSet();

            // 3. Загружаем трейдеров
            var traders = await _traderRepo.GetByIdsAsync(traderIds);
            return traders.ToDictionary(t => t.TelegramId);
        }

        public async Task<Dictionary<long, PortfolioItem>> GetPortfoliosForTradersAsync(string symbol, IEnumerable<long> traderIds)
        {
            var portfolios = await _portfolioRepo.GetByTradersAndSymbolAsync(traderIds, symbol);
            return portfolios.ToDictionary(p => p.TraderTelegramId);
        }

        public async Task<CharacterToken> GetTokenAsync(string symbol)
        {
            return await _tokenRepo.GetByIdAsync(symbol)
                ?? throw new Exception($"Токен {symbol} не найден");
        }

        public async Task SaveTradingResultAsync(TradingResult result)
        {
            try
            {
                if (result.Trades.Count != 0)
                    await _tradeRepo.AddRangeAsync(result.Trades);

                if (result.UpdatedOrders.Count != 0)
                    await _orderRepo.UpdateRangeAsync(result.UpdatedOrders);

                if (result.UpdatedTraders.Count != 0)
                    await _traderRepo.UpdateRangeAsync(result.UpdatedTraders);

                if (result.UpdatedPortfolios.Count != 0)
                    await _portfolioRepo.UpdateRangeAsync(result.UpdatedPortfolios);

                if (result.UpdatedToken != null)
                    await _tokenRepo.UpdateAsync(result.UpdatedToken);

                if (result.OrderToAdd != null)
                    await _orderRepo.AddAsync(result.OrderToAdd);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения TradingResult: {ex.Message}");
                throw;
            }

        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var result = await action();
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch (Exception er)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
