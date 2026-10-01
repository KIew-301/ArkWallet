using ArkWallet.Core.General.Application.Contracts.Other;
using ArkWallet.Core.General.Domain.ValueObjects;
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
                candles.Add(PriceCandle.Create(symbol, BasePrice, now));
        }

        await db.CharacterTokens.AddRangeAsync(tokens);
        await db.PriceCandles.AddRangeAsync(candles);
        await db.SaveChangesAsync();
    }

    public static async Task<Trader> SeedTraderAsync(ArkWalletDbContext db, long telegramId, decimal balance = 10_000m)
    {
        var trader = Trader.Create(null, false, telegramId);
        trader.Balance = balance;

        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        return trader;
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

        for (int i = 0; i < traderCount; i++)
        {
            var symbol = Symbol(i);
            var telegramId = 10_000L + i;
            var trader = Trader.Create(null, false, telegramId);
            trader.Balance = 1000m * (i + 1);

            traders.Add(trader);
            tokens.Add(CharacterToken.Create(symbol, $"Token {i}", CharacterRarity.FourStar, BasePrice, 1_000_000, $"img{i}.png", $"icon{i}.png"));
            candles.Add(PriceCandle.Create(symbol, BasePrice, now));
        }

        await db.Traders.AddRangeAsync(traders);
        await db.CharacterTokens.AddRangeAsync(tokens);
        await db.PriceCandles.AddRangeAsync(candles);
        await db.SaveChangesAsync();

        var portfolios = new List<PortfolioItem>();
        for (int i = 0; i < traderCount; i++)
            portfolios.Add(PortfolioItem.Create(traders[i].Id, Symbol(i), 10, BasePrice));

        await db.PortfolioItems.AddRangeAsync(portfolios);
        await db.SaveChangesAsync();
    }

    public static async Task SeedMarketMakerScenarioAsync(ArkWalletDbContext db, int tokenCount)
    {
        var now = DateTime.UtcNow.AddMinutes(-10);
        var traders = new List<Trader>();
        var tokens = new List<CharacterToken>();
        var candles = new List<PriceCandle>();

        for (int i = 0; i < tokenCount; i++)
        {
            var symbol = Symbol(i);

            var buyerTrader = Trader.Create($"MarketMakerBot_{symbol}_Buyer", isBot: true);
            buyerTrader.Balance = 100_000_000m;
            traders.Add(buyerTrader);

            var sellerTrader = Trader.Create($"MarketMakerBot_{symbol}_Seller", isBot: true);
            sellerTrader.Balance = 100_000_000m;
            traders.Add(sellerTrader);

            tokens.Add(CharacterToken.Create(symbol, $"Token {i}", CharacterRarity.FourStar, BasePrice, 1_000_000, $"img{i}.png", $"icon{i}.png"));
            candles.Add(PriceCandle.Create(symbol, BasePrice, now));
        }

        await db.Traders.AddRangeAsync(traders);
        await db.CharacterTokens.AddRangeAsync(tokens);
        await db.PriceCandles.AddRangeAsync(candles);
        await db.SaveChangesAsync();

        var bots = new List<MarketMakerBotRecord>();
        var portfolios = new List<PortfolioItem>();
        var sellOrders = new List<TradeOrder>();

        for (int i = 0; i < tokenCount; i++)
        {
            var symbol = Symbol(i);
            var buyerTrader = traders[i * 2];
            var sellerTrader = traders[i * 2 + 1];

            bots.Add(MarketMakerBotRecord.Create(buyerTrader.Id, symbol, BotRole.Buyer, 10m));
            bots.Add(MarketMakerBotRecord.Create(sellerTrader.Id, symbol, BotRole.Seller, 10m));
            portfolios.Add(PortfolioItem.Create(buyerTrader.Id, symbol, 1_000_000, BasePrice));
            portfolios.Add(PortfolioItem.Create(sellerTrader.Id, symbol, 1_000_000, BasePrice));
            sellOrders.Add(TradeOrder.Create(OrderType.Sell, symbol, sellerTrader.Id, BasePrice, 100_000));
            sellOrders.Add(TradeOrder.Create(OrderType.Sell, symbol, sellerTrader.Id, BasePrice + 1, 100_000));
        }

        await db.MarketMakerBots.AddRangeAsync(bots);
        await db.PortfolioItems.AddRangeAsync(portfolios);
        await db.TradeOrders.AddRangeAsync(sellOrders);
        await db.SaveChangesAsync();
    }
}
