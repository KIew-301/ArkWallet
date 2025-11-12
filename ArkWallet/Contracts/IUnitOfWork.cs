using ArkWallet.Entities;
using ArkWallet.ValueObjects;

namespace ArkWallet.Contracts
{
    internal interface IUnitOfWork : IDisposable
    {
        // Доступ к репозиториям
        ITraderRepository Traders { get; }
        ITradeOrderRepository Orders { get; }
        IPortfolioItemRepository Portfolios { get; }
        ICharacterTokenRepository Tokens { get; }
        ITradeRepository Trades { get; }

        // Управление транзакциями
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
        Task SaveChangesAsync();
    }
}
