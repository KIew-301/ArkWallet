using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;

namespace ArkWallet.PerformanceTests.E2e;

internal static class E2eSeed
{
    public static async Task DashboardAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 50);
        await GatesSeed.SeedTraderPortfolioAsync(db, E2eConfig.TraderId, E2eConfig.Symbol, 1_000_000);
    }

    public static async Task TradingAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
    }

    public static async Task WizardAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 50);
    }

    public static async Task AdminAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.MainAdminId, 100_000m);
        await GatesSeed.SeedMarketMakerScenarioAsync(db, 5);
    }

    public static async Task TelegramLevelAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.MainAdminId, 100_000m);
        await GatesSeed.SeedLeaderboardAsync(db, 10);
    }
}
