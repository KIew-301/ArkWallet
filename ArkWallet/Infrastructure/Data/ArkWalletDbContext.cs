using ArkWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Infrastructure.Data
{
    internal class ArkWalletDbContext : DbContext
    {
        public DbSet<Trader> Traders { get; set; }
        public DbSet<CharacterToken> CharacterTokens { get; set; }
        public DbSet<PortfolioItem> PortfolioItems { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<TradeOrder> TradeOrders { get; set; }
        public DbSet<PriceCandle> PriceCandles { get; set; }

        public ArkWalletDbContext(DbContextOptions<ArkWalletDbContext> options) : base(options)
        {
        }
    }
}
