using ArkWallet.Data;
using ArkWallet.Repositories;
using ArkWallet.ValueObjects;

namespace ArkWallet.Infrastructure
{
    internal class UnitOfWork
    {
        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;
        private readonly ArkWalletDbContext _dbContext;

        public UnitOfWork(TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo,
            ArkWalletDbContext dbContext)
        {
            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
            _dbContext = dbContext;
        }

        public async Task<OrderResult> PlaceOrder()
        {
            using var transaction = new _dbContext.Transaction();
        }
    }
}
