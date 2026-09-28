using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests.Core.General.Application.Services.Orchestrators;

public class MarketDataProviderTest : IDisposable
{
    private readonly List<IAsyncDisposable> _disposables = new();
    private static readonly IReadOnlyCollection<string> Tkn01Symbols = new[] { "TKN01" };

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var d in _disposables)
            d.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task<ArkWalletDbContext> CreateDbAsync()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task GridMask_WithoutDayAgo_ReturnsSnapshot()
    {
        var db = await CreateDbAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 111m, DateTime.UtcNow);

        var provider = new MarketDataProvider(db);
        var mask = MarketDataMasks.CurrentPrice | MarketDataMasks.ExistingLevels;

        var result = await provider.LoadAsync(mask, Tkn01Symbols, CancellationToken.None);

        Assert.Contains(result, kvp => string.Equals(kvp.Key, "TKN01", StringComparison.OrdinalIgnoreCase));
        var conditions = result["TKN01"];
        Assert.Equal(10000m, conditions.CurrentPrice);
        Assert.Null(conditions.DayAgoPrice);
        Assert.Equal(100m, conditions.BasePrice);
        Assert.Empty(conditions.ExistingLevels);
    }

    [Fact]
    public async Task DayAgoMask_ReturnsDayAgoPrice()
    {
        var db = await CreateDbAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 555m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 111m, DateTime.UtcNow);

        var provider = new MarketDataProvider(db);
        var mask = MarketDataMasks.CurrentPrice | MarketDataMasks.DayAgoPrice;

        var result = await provider.LoadAsync(mask, Tkn01Symbols, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(555m, result["TKN01"].DayAgoPrice);
    }

    [Fact]
    public async Task DayAgoMissing_SkipsSymbol()
    {
        var db = await CreateDbAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 111m, DateTime.UtcNow);

        var provider = new MarketDataProvider(db);
        var mask = MarketDataMasks.CurrentPrice | MarketDataMasks.DayAgoPrice;

        var result = await provider.LoadAsync(mask, Tkn01Symbols, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExistingLevels_AreLoaded_FromActiveOrders()
    {
        var db = await CreateDbAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 111m, DateTime.UtcNow);

        await HelpMethods.RegisterTrader(db, 5001);

        var buyOrder = TradeOrder.Create(OrderType.Buy, "TKN01", 5001, 90m, 1);
        buyOrder.Status = OrderStatus.Active;
        await db.TradeOrders.AddAsync(buyOrder);

        var sellOrder = TradeOrder.Create(OrderType.Sell, "TKN01", 5001, 110m, 1);
        sellOrder.Status = OrderStatus.Active;
        await db.TradeOrders.AddAsync(sellOrder);
        await db.SaveChangesAsync();

        var provider = new MarketDataProvider(db);
        var mask = MarketDataMasks.CurrentPrice | MarketDataMasks.ExistingLevels;

        var result = await provider.LoadAsync(mask, Tkn01Symbols, CancellationToken.None);

        Assert.Single(result);
        var levels = result["TKN01"].ExistingLevels;
        Assert.Equal(2, levels.Count);
        Assert.Contains(levels, l => l.Price == 90m && l.IsBuy);
        Assert.Contains(levels, l => l.Price == 110m && !l.IsBuy);
    }

    [Fact]
    public async Task EmptySymbols_ReturnsEmpty()
    {
        var db = await CreateDbAsync();

        var provider = new MarketDataProvider(db);
        var mask = MarketDataMasks.CurrentPrice;

        var result = await provider.LoadAsync(mask, Array.Empty<string>(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task NoCandles_NoBasePrice_SkipsSymbol()
    {
        var db = await CreateDbAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);

        var provider = new MarketDataProvider(db);
        var mask = MarketDataMasks.CurrentPrice;

        var result = await provider.LoadAsync(mask, Tkn01Symbols, CancellationToken.None);

        Assert.Empty(result);
    }
}
