using ArkWallet.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Data
{
    internal class ArkWalletDbContext : DbContext
    {
        public DbSet<Trader> Traders { get; set; }
        public DbSet<CharacterToken> CharacterTokens { get; set; }
        public DbSet<PortfolioItem> PortfolioItems { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<TradeOrder> TradeOrders { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=arkwallet.db");
        }
    }
}
