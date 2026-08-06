namespace ArkWallet.PerformanceTests.Gates;

internal sealed record Budget(int Queries, int TimeMs, int? SaveChanges);

internal static class GateBudgets
{
    public static readonly Budget TokenQuery50T = new(110, 30, null);
    public static readonly Budget BalanceMainChanges = new(5, 355, null);
    public static readonly Budget BalanceTotalChanges = new(5, 25, null);
    public static readonly Budget LeadersTop50T = new(110, 50, null);
    public static readonly Budget OrderCreateBuy = new(10, 25, null);
    public static readonly Budget OrderCreateSell = new(11, 25, null);
    public static readonly Budget MarketMakerTick10T = new(6000, 3259, 600);
    public static readonly Budget MarketMakerTick20T = new(12000, 5605, 1200);
    public static readonly Budget E2eDashboardFlow = new(117, 50, null);
    public static readonly Budget E2eTradingFlow = new(40, 2000, null);
    public static readonly Budget E2eCacheCheck = new(108, 40, null);
    public static readonly Budget E2eBotWizardFlow = new(238, 4000, null);
    public static readonly Budget E2eBotAdminFlow = new(40, 2000, null);
    public static readonly Budget E2eTelegramBotLevel = new(102, 40, null);
}
