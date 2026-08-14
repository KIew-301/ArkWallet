using ArkWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Infrastructure.Data;

internal class ArkWalletDbContext : DbContext
{
    public DbSet<AppState> AppStates { get; set; }
    public DbSet<Trader> Traders { get; set; }
    public DbSet<CharacterToken> CharacterTokens { get; set; }
    public DbSet<PortfolioItem> PortfolioItems { get; set; }
    public DbSet<Trade> Trades { get; set; }
    public DbSet<TradeOrder> TradeOrders { get; set; }
    public DbSet<PriceCandle> PriceCandles { get; set; }
    public DbSet<BalanceSnapshot> BalanceSnapshots { get; set; }
    public DbSet<MarketMakerBot> MarketMakerBots { get; set; }
    public DbSet<MiningMachine> MiningMachines { get; set; }
    public DbSet<MiningMachineRule> MiningMachineRules { get; set; }
    public DbSet<MiningMachineSlot> MiningMachineSlots { get; set; }
    public DbSet<MiningGlobalRule> MiningGlobalRules { get; set; }

    public ArkWalletDbContext(DbContextOptions<ArkWalletDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BalanceSnapshot>()
            .HasIndex(b => new { b.TraderId, b.SnapshotDateTime });

        modelBuilder.Entity<MiningMachine>(machine =>
        {
            machine.Property(m => m.Type).HasConversion<string>();
            machine.HasIndex(m => m.Name).IsUnique();
            machine.HasMany(m => m.MiningMachineRules)
                .WithOne(r => r.MiningMachine)
                .HasForeignKey(r => r.MiningMachineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MiningMachineRule>(rule =>
        {
            rule.HasIndex(r => new { r.MiningMachineId, r.CharacterTokenId }).IsUnique();
            rule.HasOne(r => r.CharacterToken)
                .WithMany()
                .HasForeignKey(r => r.CharacterTokenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MiningMachineSlot>(slot =>
        {
            slot.Property(s => s.Status).HasConversion<string>();
            slot.HasIndex(s => s.TraderId);
            slot.HasIndex(s => s.Status);
            slot.HasOne(s => s.MiningMachine)
                .WithMany()
                .HasForeignKey(s => s.MiningMachineId)
                .OnDelete(DeleteBehavior.Restrict);
            slot.HasOne(s => s.MachineRule)
                .WithMany()
                .HasForeignKey(s => s.MachineRuleId)
                .OnDelete(DeleteBehavior.Restrict);
            slot.HasOne(s => s.MiningGlobalRule)
                .WithMany()
                .HasForeignKey(s => s.MiningGlobalRuleId)
                .OnDelete(DeleteBehavior.Restrict);
            slot.HasOne(s => s.Token)
                .WithMany()
                .HasForeignKey(s => s.TokenId)
                .OnDelete(DeleteBehavior.Restrict);
            slot.HasOne<Trader>()
                .WithMany()
                .HasForeignKey(s => s.TraderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MiningGlobalRule>(rule =>
        {
            rule.HasIndex(r => r.TokenId).IsUnique();
            rule.HasOne(r => r.CharacterToken)
                .WithMany()
                .HasForeignKey(r => r.TokenId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
