using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;

namespace ArkWallet.PerformanceTests.E2e;

internal static class E2eSeed
{
    public const long CounterpartId = 102;
    public const int HeavyTokenCount = 100;
    public const int HeavyTokenCountBalance = 200;
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

    public static async Task HeavyOrdersGetAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 1_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, HeavyTokenCount);

        var orders = new List<TradeOrder>();
        for (int i = 0; i < 10_000; i++)
        {
            var order = TradeOrder.Create(
                i % 2 == 0 ? OrderType.Buy : OrderType.Sell,
                GatesSeed.Symbol(i % HeavyTokenCount),
                E2eConfig.TraderId,
                500m + i,
                (i % 100) + 1);

            var bucket = i % 10;
            order.Status = bucket < 4 ? OrderStatus.Active : bucket < 7 ? OrderStatus.Filled : OrderStatus.Cancelled;
            if (order.Status == OrderStatus.Filled)
                order.FilledQuantity = order.Quantity;

            order.CreatedAt = DateTime.UtcNow.AddMinutes(-i);
            orders.Add(order);
        }

        await db.TradeOrders.AddRangeAsync(orders);
        await db.SaveChangesAsync();
    }

    public static async Task HeavyOrderCreateAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000_000m);
        await GatesSeed.SeedTraderAsync(db, CounterpartId, 100_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, CounterpartId, E2eConfig.Symbol, 100_000);

        var asks = new List<TradeOrder>();
        for (int i = 0; i < 2000; i++)
            asks.Add(TradeOrder.Create(OrderType.Sell, E2eConfig.Symbol, CounterpartId, i + 1, 10));

        await db.TradeOrders.AddRangeAsync(asks);
        await db.SaveChangesAsync();
    }

    public static async Task HeavyOrdersCancelAllAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, HeavyTokenCount);

        var portfolios = new List<PortfolioItem>();
        for (int i = 0; i < HeavyTokenCount; i++)
            portfolios.Add(PortfolioItem.Create(E2eConfig.TraderId, GatesSeed.Symbol(i), 1000, 1000m));
        await db.PortfolioItems.AddRangeAsync(portfolios);

        var orders = new List<TradeOrder>();
        for (int i = 0; i < 2000; i++)
        {
            var type = i % 5 == 0 ? OrderType.Buy : OrderType.Sell;
            orders.Add(TradeOrder.Create(type, GatesSeed.Symbol(i % HeavyTokenCount), E2eConfig.TraderId, 1000m, 10));
        }

        await db.TradeOrders.AddRangeAsync(orders);
        await db.SaveChangesAsync();
    }

    public static async Task HeavyTokensGetAsync(ArkWalletDbContext db)
        => await GatesSeed.SeedTokenCatalogAsync(db, 500);

    public static async Task HeavyCandleGetAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 1, withCandles: false);

        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-100).AddMinutes(-5);
        var windowEnd = now.AddMinutes(-10);
        var step = (windowEnd - windowStart) / 100_000;

        var candles = new List<PriceCandle>(100_000);
        for (int i = 0; i < 100_000; i++)
            candles.Add(PriceCandle.CreateNew(E2eConfig.Symbol, 1000m + (i % 100), windowStart + step * i));

        await db.PriceCandles.AddRangeAsync(candles);
        await db.SaveChangesAsync();
    }

    public static async Task HeavyBalanceGetAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, HeavyTokenCountBalance);

        var portfolios = new List<PortfolioItem>();
        for (int i = 0; i < 500; i++)
            portfolios.Add(PortfolioItem.Create(E2eConfig.TraderId, GatesSeed.Symbol(i % HeavyTokenCountBalance), 1000, 1000m));
        await db.PortfolioItems.AddRangeAsync(portfolios);

        var orders = new List<TradeOrder>();
        for (int i = 0; i < 1000; i++)
        {
            var type = i % 2 == 0 ? OrderType.Buy : OrderType.Sell;
            orders.Add(TradeOrder.Create(type, GatesSeed.Symbol(i % HeavyTokenCountBalance), E2eConfig.TraderId, 1000m, 10));
        }
        await db.TradeOrders.AddRangeAsync(orders);

        var now = DateTime.UtcNow;
        var snapshots = new List<BalanceSnapshot>(10_000);
        for (int i = 0; i < 10_000; i++)
            snapshots.Add(BalanceSnapshot.Create(E2eConfig.TraderId, 100_000m, 100_000m, 0m, 0m, 0m, now.AddHours(-i)));

        await db.BalanceSnapshots.AddRangeAsync(snapshots);
        await db.SaveChangesAsync();
    }

    public static async Task HeavyTradesGetAsync(ArkWalletDbContext db)
    {
        await GatesSeed.SeedTraderAsync(db, E2eConfig.TraderId, 100_000m);
        await GatesSeed.SeedTraderAsync(db, CounterpartId, 100_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, HeavyTokenCount);

        var now = DateTime.UtcNow;
        var trades = new List<Trade>(20_000);
        for (int i = 0; i < 20_000; i++)
        {
            var isBuyer = i % 2 == 0;
            trades.Add(new Trade
            {
                BuyerId = isBuyer ? E2eConfig.TraderId : CounterpartId,
                SellerId = isBuyer ? CounterpartId : E2eConfig.TraderId,
                CharacterTokenId = GatesSeed.Symbol(i % HeavyTokenCount),
                Price = 1000m + (i % 500),
                Quantity = (i % 100) + 1,
                ExecutedAt = now.AddMinutes(-i)
            });
        }

        await db.Trades.AddRangeAsync(trades);
        await db.SaveChangesAsync();
    }
}
