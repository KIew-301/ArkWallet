using ArkWallet.Application.Contracts;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Infrastructure.Services
{
    internal class UnitOfWork : IUnitOfWork
    {
        private readonly ITraderRepository _traderRepo;
        private readonly ICharacterTokenRepository _tokenRepo;
        private readonly IPortfolioItemRepository _portfolioRepo;
        private readonly ITradeOrderRepository _orderRepo;
        private readonly ITradeRepository _tradeRepo;
        private readonly ArkWalletDbContext _dbContext;

        // 🔥 Публичные свойства через интерфейсы
        public ITraderRepository Traders => _traderRepo;
        public ICharacterTokenRepository Tokens => _tokenRepo;
        public IPortfolioItemRepository Portfolios => _portfolioRepo;
        public ITradeOrderRepository Orders => _orderRepo;
        public ITradeRepository Trades => _tradeRepo;

        public UnitOfWork(
            ITraderRepository traderRepo,
            ICharacterTokenRepository tokenRepo,
            IPortfolioItemRepository portfolioRepo,
            ITradeOrderRepository orderRepo,
            ITradeRepository tradeRepo,
            ArkWalletDbContext dbContext)
        {
            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
            _orderRepo = orderRepo;
            _tradeRepo = tradeRepo;
            _dbContext = dbContext;
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

        public void Dispose()
        {
            _dbContext?.Dispose();
            Console.WriteLine("UnitOfWork disposed - соединение с БД закрыто");
        }
    }
}
