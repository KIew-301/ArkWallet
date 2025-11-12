using ArkWallet.Repositories;

namespace ArkWallet.Infrastructure
{
    internal class UnitOfWork
    {
        private readonly TraderRepository _traderRepo;
        private readonly CharacterTokenRepository _tokenRepo;
        private readonly PortfolioItemRepository _portfolioRepo;

        public UnitOfWork(TraderRepository traderRepo,
            CharacterTokenRepository tokenRepo,
            PortfolioItemRepository portfolioRepo)
        {
            _traderRepo = traderRepo;
            _tokenRepo = tokenRepo;
            _portfolioRepo = portfolioRepo;
        }
    }
}
