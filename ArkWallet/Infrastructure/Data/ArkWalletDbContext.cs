using ArkWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

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
    public DbSet<MiningMachineSlotRule> MiningMachineSlotRules { get; set; }
    public DbSet<MiningGlobalRule> MiningGlobalRules { get; set; }
    public DbSet<AccessSetting> AccessSettings { get; set; }
    public DbSet<Gift> Gifts { get; set; }

    public ArkWalletDbContext(DbContextOptions<ArkWalletDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BalanceSnapshot>()
            .HasIndex(b => new { b.TraderId, b.SnapshotDateTime });

        modelBuilder.Entity<MiningMachine>(machine =>
        {
            machine.HasKey(m => m.Id);
            machine.Property(m => m.Type).HasConversion<string>();
            machine.HasIndex(m => m.Name).IsUnique();
            machine.HasMany(m => m.MiningMachineRules)
                .WithOne(r => r.MiningMachine)
                .HasForeignKey(r => r.MiningMachineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MiningMachineRule>(rule =>
        {
            rule.HasKey(r => r.Id);
            rule.HasIndex(r => new { r.MiningMachineId, r.CharacterTokenId }).IsUnique();
            rule.HasOne(r => r.CharacterToken)
                .WithMany()
                .HasForeignKey(r => r.CharacterTokenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MiningMachineSlot>(slot =>
        {
            slot.HasKey(s => s.Id);
            slot.Property(s => s.Status).HasConversion<string>();
            slot.Property(s => s.Type).HasConversion<string>();
            slot.HasIndex(s => s.TraderId);
            slot.HasIndex(s => s.Status);
            slot.HasMany(s => s.MiningMachineSlotRules)
                .WithOne(r => r.MiningMachineSlot)
                .HasForeignKey(r => r.MiningMachineSlotId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<MiningMachineSlotRule>(rule =>
        {
            rule.HasKey(r => r.Id);
            rule.HasIndex(r => new { r.MiningMachineSlotId, r.CharacterTokenId }).IsUnique();
            rule.HasOne(r => r.CharacterToken)
                .WithMany()
                .HasForeignKey(r => r.CharacterTokenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MiningGlobalRule>(rule =>
        {
            rule.HasKey(r => r.Id);
            rule.HasIndex(r => r.TokenId).IsUnique();
            rule.HasOne(r => r.CharacterToken)
                .WithMany()
                .HasForeignKey(r => r.TokenId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        var longListComparer = new ValueComparer<List<long>>(
            (a, b) => SequenceEqual(a, b),
            a => a.Aggregate(0, (acc, v) => HashCode.Combine(acc, v.GetHashCode())),
            a => a.ToList());

        modelBuilder.Entity<AccessSetting>(setting =>
        {
            setting.Property(s => s.WhiteList)
                .HasConversion(
                    v => SerializeList(v),
                    v => DeserializeList(v),
                    longListComparer);
            setting.Property(s => s.BlackList)
                .HasConversion(
                    v => SerializeList(v),
                    v => DeserializeList(v),
                    longListComparer);
            setting.Property(s => s.GroupWhiteList)
                .HasConversion(
                    v => SerializeList(v),
                    v => DeserializeList(v),
                    longListComparer);
            setting.Property(s => s.GroupBlackList)
                .HasConversion(
                    v => SerializeList(v),
                    v => DeserializeList(v),
                    longListComparer);
        });
    }

    private static string SerializeList(List<long> list) => JsonSerializer.Serialize(list);
    private static List<long> DeserializeList(string json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<List<long>>(json) ?? new();
    private static bool SequenceEqual(List<long> a, List<long> b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);
}
