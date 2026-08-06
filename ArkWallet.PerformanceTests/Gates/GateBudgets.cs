namespace ArkWallet.PerformanceTests.Gates;

internal sealed record Budget(int Queries, int TimeMs, int? SaveChanges, int? Rows = null);

internal static class GateBudgets
{
    public static readonly Budget TokenQuery50T = new(110, 30, null, 158);
    public static readonly Budget BalanceMainChanges = new(5, 355, null, 4);
    public static readonly Budget BalanceTotalChanges = new(5, 25, null, 4);
    public static readonly Budget LeadersTop50T = new(110, 50, null, 158);
    public static readonly Budget OrderCreateBuy = new(10, 25, null, 7);
    public static readonly Budget OrderCreateSell = new(11, 25, null, 8);
    public static readonly Budget MarketMakerTick10T = new(6000, 3259, 600, 12615);
    public static readonly Budget MarketMakerTick20T = new(12000, 5605, 1200, 25236);
    public static readonly Budget E2eDashboardFlow = new(117, 50, null, 165);
    public static readonly Budget E2eTradingFlow = new(40, 2000, null, 66);
    public static readonly Budget E2eCacheCheck = new(108, 40, null, 159);
    public static readonly Budget E2eBotWizardFlow = new(238, 4000, null, 369);
    public static readonly Budget E2eBotAdminFlow = new(40, 2000, null, 23);
    public static readonly Budget E2eTelegramBotLevel = new(102, 40, null, 141);

    public static readonly IReadOnlyDictionary<string, Budget> ById = new Dictionary<string, Budget>(StringComparer.OrdinalIgnoreCase)
    {
        ["token-query-50t"] = TokenQuery50T,
        ["balance-main-changes"] = BalanceMainChanges,
        ["balance-total-changes"] = BalanceTotalChanges,
        ["leaders-top-50t"] = LeadersTop50T,
        ["order-create-buy"] = OrderCreateBuy,
        ["order-create-sell"] = OrderCreateSell,
        ["market-maker-tick-10t"] = MarketMakerTick10T,
        ["market-maker-tick-20t"] = MarketMakerTick20T,
        ["e2e-dashboard-flow"] = E2eDashboardFlow,
        ["e2e-trading-flow"] = E2eTradingFlow,
        ["e2e-cache-check"] = E2eCacheCheck,
        ["e2e-bot-wizard-flow"] = E2eBotWizardFlow,
        ["e2e-bot-admin-flow"] = E2eBotAdminFlow,
        ["e2e-telegram-bot-level"] = E2eTelegramBotLevel,
    };
}
