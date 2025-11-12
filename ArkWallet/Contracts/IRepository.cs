using ArkWallet.Entities;
using ArkWallet.ValueObjects;

namespace ArkWallet.Contracts
{
    internal interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);
        Task<IEnumerable<T>> GetAllAsync();
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task UpdateAsync(T entity);
        Task UpdateRangeAsync(IEnumerable<T> entities);
        void RemoveAsync(T entity);
        void RemoveRangeAsync(IEnumerable<T> entities);
        Task<bool> ExistsAsync(object id);
    }

    internal interface ITradeOrderRepository : IRepository<TradeOrder>
    {
        // Специфичные методы
        Task<TradeOrder[]> GetActiveBySymbolAsync(string symbol);
        Task<TradeOrder[]> GetByTraderAsync(long traderId);
        Task<TradeOrder[]> GetPendingByTraderAsync(long traderId);
        Task CancelOrderAsync(string orderId);
    }

    internal interface ITraderRepository : IRepository<Trader>
    {
        // Специфичные методы
        Task<Trader?> GetByTelegramIdAsync(long telegramId);
        Task<List<Trader>> GetByIdsAsync(IEnumerable<long> telegramIds);
        Task<bool> ExistsByTelegramIdAsync(long telegramId);
        Task UpdateBalanceAsync(long telegramId, decimal newBalance);
    }

    internal interface IPortfolioItemRepository : IRepository<PortfolioItem>
    {
        // Специфичные методы
        Task<PortfolioItem?> GetByTraderAndSymbolAsync(long traderId, string symbol);
        Task<List<PortfolioItem>> GetByTraderAsync(long traderId);
        Task<List<PortfolioItem>> GetByTradersAndSymbolAsync(IEnumerable<long> traderIds, string symbol);
        Task<decimal> GetTotalPortfolioValueAsync(long traderId);
        Task AddOrUpdateAsync(long traderId, string symbol, int quantity, decimal price);
    }

    internal interface ITradeRepository : IRepository<Trade>
    {
        // Специфичные методы
        Task<Trade[]> GetByTraderAsync(long traderId);
        Task<Trade[]> GetBySymbolAsync(string symbol);
        Task<Trade[]> GetRecentTradesAsync(int count);
    }

    internal interface ICharacterTokenRepository : IRepository<CharacterToken>
    {
        // Специфичные методы
        Task<CharacterToken?> GetBySymbolAsync(string symbol);
        Task<List<CharacterToken>> GetActiveTokensAsync();
        Task<List<CharacterToken>> GetByRarityAsync(CharacterRarity rarity);
    }
}
