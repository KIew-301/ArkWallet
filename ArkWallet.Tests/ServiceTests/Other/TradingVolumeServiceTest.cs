using ArkWallet.Application.Services.Other;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Other;

public class TradingVolumeServiceTest
{
    // --- GetTokenVolumeAsync ---

    [Fact]
    public async Task GetTokenVolumeAsync_NoTrades_ReturnsZero()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(0m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_SingleTrade_ReturnsCorrectVolume()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_MultipleTradesSameToken_SumsVolume()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 10, 20, "ZZZ", quantity: 3, price: 200m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(1100m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_MultipleTokens_OnlyCountsSpecifiedSymbol()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 10, 20, "YYY", quantity: 10, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_IncludeBotsTrue_IncludesBotTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 101, 200, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_IncludeBotsFalse_ExcludesBotTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 101, 200, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(0m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_IncludeBotsFalse_BuyerBotSellerNonBot_Excludes()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 101, 10, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(0m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_IncludeBotsFalse_SellerBotBuyerNonBot_Excludes()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 101, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(0m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_IncludeBotsFalse_BothNonBot_Includes()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_PeriodDays30_OnlyCountsRecentTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m, executedAt: DateTime.UtcNow.AddDays(-10));
        await InsertTrade(db, 10, 20, "ZZZ", quantity: 3, price: 200m, executedAt: DateTime.UtcNow.AddDays(-60));

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 30, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTokenVolumeAsync_PeriodDays0_IncludesAllTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m, executedAt: DateTime.UtcNow.AddDays(-10));
        await InsertTrade(db, 10, 20, "ZZZ", quantity: 3, price: 200m, executedAt: DateTime.UtcNow.AddDays(-60));

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTokenVolumeAsync("ZZZ", 0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(1100m, volume);
    }

    // --- GetTotalVolumeAsync ---

    [Fact]
    public async Task GetTotalVolumeAsync_NoTrades_ReturnsZero()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTotalVolumeAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(0m, volume);
    }

    [Fact]
    public async Task GetTotalVolumeAsync_SingleTrade_ReturnsCorrectVolume()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTotalVolumeAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTotalVolumeAsync_MultipleTokens_SumsAll()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 10, 20, "YYY", quantity: 10, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTotalVolumeAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(1000m, volume);
    }

    [Fact]
    public async Task GetTotalVolumeAsync_IncludeBotsFalse_ExcludesBotTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 101, 200, "YYY", quantity: 10, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTotalVolumeAsync(0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    [Fact]
    public async Task GetTotalVolumeAsync_IncludeBotsTrue_IncludesBotTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 101, 200, "YYY", quantity: 10, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTotalVolumeAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(1000m, volume);
    }

    [Fact]
    public async Task GetTotalVolumeAsync_PeriodDays7_OnlyCountsRecentTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m, executedAt: DateTime.UtcNow.AddDays(-3));
        await InsertTrade(db, 10, 20, "YYY", quantity: 10, price: 50m, executedAt: DateTime.UtcNow.AddDays(-30));

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetTotalVolumeAsync(7, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var volume));
        Assert.Equal(500m, volume);
    }

    // --- GetVolumePerTokenAsync ---

    [Fact]
    public async Task GetVolumePerTokenAsync_NoTrades_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetVolumePerTokenAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetVolumePerTokenAsync_SingleToken_ReturnsOneEntry()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetVolumePerTokenAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal("ZZZ", data[0].Symbol);
        Assert.Equal(500m, data[0].Volume);
    }

    [Fact]
    public async Task GetVolumePerTokenAsync_MultipleTokens_ReturnsSortedByVolumeDescending()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "AAA", quantity: 1, price: 10m);
        await InsertTrade(db, 10, 20, "CCC", quantity: 10, price: 100m);
        await InsertTrade(db, 10, 20, "BBB", quantity: 5, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetVolumePerTokenAsync(0, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(3, data.Count);
        Assert.Equal("CCC", data[0].Symbol);
        Assert.Equal("BBB", data[1].Symbol);
        Assert.Equal("AAA", data[2].Symbol);
    }

    [Fact]
    public async Task GetVolumePerTokenAsync_IncludeBotsFalse_ExcludesBotTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 101, 200, "YYY", quantity: 10, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetVolumePerTokenAsync(0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal("ZZZ", data[0].Symbol);
    }

    [Fact]
    public async Task GetVolumePerTokenAsync_PeriodDays14_OnlyCountsRecentTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m, executedAt: DateTime.UtcNow.AddDays(-5));
        await InsertTrade(db, 10, 20, "YYY", quantity: 10, price: 50m, executedAt: DateTime.UtcNow.AddDays(-30));

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetVolumePerTokenAsync(14, includeBots: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal("ZZZ", data[0].Symbol);
        Assert.Equal(500m, data[0].Volume);
    }

    [Fact]
    public async Task GetVolumePerTokenAsync_MixedBotAndNonBot_IncludeBotsFalse_OnlyCountsNonBot()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await InsertTrade(db, 10, 20, "ZZZ", quantity: 5, price: 100m);
        await InsertTrade(db, 10, 20, "ZZZ", quantity: 3, price: 200m);
        await InsertTrade(db, 101, 200, "ZZZ", quantity: 10, price: 50m);

        var service = new TradingVolumeService(db, NullLogger<TradingVolumeService>.Instance);

        var result = await service.GetVolumePerTokenAsync(0, includeBots: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal(1100m, data[0].Volume);
    }

    // --- Helper ---

    private static async Task InsertTrade(
        ArkWalletDbContext db,
        long buyerId,
        long sellerId,
        string symbol,
        int quantity,
        decimal price,
        DateTime? executedAt = null)
    {
        if (await db.Traders.FindAsync(buyerId) == null)
            await HelpMethods.RegisterTrader(db, buyerId, $"Bot_{buyerId}");
        if (await db.Traders.FindAsync(sellerId) == null)
            await HelpMethods.RegisterTrader(db, sellerId, $"Bot_{sellerId}");
        if (!await db.CharacterTokens.AnyAsync(t => t.Symbol == symbol))
            await HelpMethods.CreateToken(db, symbol);

        var trade = new ArkWallet.Domain.Entities.Trade
        {
            BuyerId = buyerId,
            SellerId = sellerId,
            CharacterTokenId = symbol,
            Price = price,
            Quantity = quantity,
            ExecutedAt = executedAt ?? DateTime.UtcNow
        };

        db.Trades.Add(trade);
        await db.SaveChangesAsync();
    }
}
