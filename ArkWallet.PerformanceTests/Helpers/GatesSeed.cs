using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.PerformanceTests.Helpers;

internal static class GatesSeed
{
    private const decimal BasePrice = 1000m;

    public static string Symbol(int index) => $"TKN{index:D3}";

    public static async Task SeedTokenCatalogAsync(ArkWalletDbContext db, int count, bool withCandles = true)
    {
        var now = DateTime.UtcNow.AddMinutes(-10);
        var tokens = new List<CharacterToken>();
        var candles = new List<PriceCandle>();

        for (int i = 0; i < count; i++)
        {
            var symbol = Symbol(i);
            tokens.Add(CharacterToken.Create(symbol, $"Token {i}", CharacterRarity.FourStar, BasePrice, 1_000_000, $"img{i}.png", $"icon{i}.png"));

            if (withCandles)
                candles.Add(PriceCandle.CreateNew(symbol, BasePrice, now));
        }

        await db.CharacterTokens.AddRangeAsync(tokens);
        await db.PriceCandles.AddRangeAsync(candles);
        await db.SaveChangesAsync();
    }

    public static async Task SeedTraderAsync(ArkWalletDbContext db, long telegramId, decimal balance = 10_000m)
    {
        var trader = Trader.Create(telegramId, $"Trader_{telegramId}");

        if (balance > Trader.GetDefaultBalance())
            trader.AddToBalance(balance - Trader.GetDefaultBalance());

        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();
    }

    public static async Task SeedTraderPortfolioAsync(ArkWalletDbContext db, long traderId, string symbol, int quantity = 1_000_000)
    {
        await db.PortfolioItems.AddAsync(PortfolioItem.Create(traderId, symbol, quantity, BasePrice));
        await db.SaveChangesAsync();
    }

    public static async Task SaveBalanceSnapshotAsync(ArkWalletDbContext db, long traderId, decimal balance, DateTime snapshotAt)
    {
        await db.BalanceSnapshots.AddAsync(BalanceSnapshot.Create(
            traderId, balance, balance, 0m, 0m, 0m, snapshotAt));
        await db.SaveChangesAsync();
    }

    public static async Task SeedLeaderboardAsync(ArkWalletDbContext db, int traderCount)
    {
        var now = DateTime.UtcNow.AddMinutes(-10);
        var traders = new List<Trader>();
        var tokens = new List<CharacterToken>();
        var candles = new List<PriceCandle>();
        var portfolios = new List<PortfolioItem>();

        for (int i = 0; i < traderCount; i++)
        {
            var symbol = Symbol(i);
            var trader = Trader.Create(i + 1, $"Trader_{i + 1}");
            trader.AddToBalance(1000m * (i + 1));

            traders.Add(trader);
            tokens.Add(CharacterToken.Create(symbol, $"Token {i}", CharacterRarity.FourStar, BasePrice, 1_000_000, $"img{i}.png", $"icon{i}.png"));
            candles.Add(PriceCandle.CreateNew(symbol, BasePrice, now));
            portfolios.Add(PortfolioItem.Create(i + 1, symbol, 10, BasePrice));
        }

        await db.Traders.AddRangeAsync(traders);
        await db.CharacterTokens.AddRangeAsync(tokens);
        await db.PriceCandles.AddRangeAsync(candles);
        await db.PortfolioItems.AddRangeAsync(portfolios);
        await db.SaveChangesAsync();
    }

    public static async Task SeedMarketMakerScenarioAsync(ArkWalletDbContext db, int tokenCount)
    {
        var now = DateTime.UtcNow.AddMinutes(-10);
        var traders = new List<Trader>();
        var tokens = new List<CharacterToken>();
        var candles = new List<PriceCandle>();
        var bots = new List<MarketMakerBot>();
        var portfolios = new List<PortfolioItem>();
        var sellOrders = new List<TradeOrder>();

        foreach (var id in new[] { 101L, 102L })
        {
            var trader = Trader.Create(id, $"MarketMakerBot_{id}");
            trader.AddToBalance(100_000_000m);
            traders.Add(trader);
        }

        for (int i = 0; i < tokenCount; i++)
        {
            var symbol = Symbol(i);

            tokens.Add(CharacterToken.Create(symbol, $"Token {i}", CharacterRarity.FourStar, BasePrice, 1_000_000, $"img{i}.png", $"icon{i}.png"));
            candles.Add(PriceCandle.CreateNew(symbol, BasePrice, now));
            bots.Add(MarketMakerBot.Create(101, symbol, BotRole.Buyer, 10m));
            bots.Add(MarketMakerBot.Create(102, symbol, BotRole.Seller, 10m));
            portfolios.Add(PortfolioItem.Create(101, symbol, 1_000_000, BasePrice));
            portfolios.Add(PortfolioItem.Create(102, symbol, 1_000_000, BasePrice));
            sellOrders.Add(TradeOrder.Create(OrderType.Sell, symbol, 102, BasePrice, 100_000));
            sellOrders.Add(TradeOrder.Create(OrderType.Sell, symbol, 102, BasePrice + 1, 100_000));
        }

        await db.Traders.AddRangeAsync(traders);
        await db.CharacterTokens.AddRangeAsync(tokens);
        await db.PriceCandles.AddRangeAsync(candles);
        await db.MarketMakerBots.AddRangeAsync(bots);
        await db.PortfolioItems.AddRangeAsync(portfolios);
        await db.TradeOrders.AddRangeAsync(sellOrders);
        await db.SaveChangesAsync();
    }
}
