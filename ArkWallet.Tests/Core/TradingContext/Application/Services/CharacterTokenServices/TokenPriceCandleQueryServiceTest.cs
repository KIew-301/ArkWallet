using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.CharacterTokenServices;

public class TokenPriceCandleQueryServiceTest
{
    [Fact]
    public async Task GetPriceCandlesAsync_WhenNoCandles_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var logger = NullLogger<TokenPriceCandleQueryService>.Instance;
        var service = new TokenPriceCandleQueryService(db, logger);

        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var result = await service.GetPriceCandlesAsync("ZZZ", start, end);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetPriceCandlesAsync_WhenCandlesExist_ReturnsAllCandles()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1000m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1200m, DateTime.UtcNow.AddDays(-1));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1500m, DateTime.UtcNow);

        var logger = NullLogger<TokenPriceCandleQueryService>.Instance;
        var service = new TokenPriceCandleQueryService(db, logger);

        var start = DateTime.UtcNow.AddDays(-3);
        var end = DateTime.UtcNow;

        var result = await service.GetPriceCandlesAsync("ZZZ", start, end);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(3, data.Count);
    }

    [Fact]
    public async Task GetPriceCandlesAsync_ReturnsCandlesInChronologicalOrder()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1200m, DateTime.UtcNow.AddDays(-1));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1000m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1500m, DateTime.UtcNow);

        var logger = NullLogger<TokenPriceCandleQueryService>.Instance;
        var service = new TokenPriceCandleQueryService(db, logger);

        var start = DateTime.UtcNow.AddDays(-3);
        var end = DateTime.UtcNow.AddDays(1);

        var result = await service.GetPriceCandlesAsync("ZZZ", start, end);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));

        var timestamps = data.Select(c => c.Timestamp).ToList();
        var sorted = timestamps.OrderBy(t => t).ToList();
        Assert.Equal(sorted, timestamps);
    }

    [Fact]
    public async Task GetPriceCandlesAsync_ReturnsCorrectCandleInfo()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var dateCreated = DateTime.UtcNow.AddDays(-1);

        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1000m, dateCreated);

        var logger = NullLogger<TokenPriceCandleQueryService>.Instance;
        var service = new TokenPriceCandleQueryService(db, logger);

        var start = DateTime.UtcNow.AddDays(-2);
        var end = DateTime.UtcNow;

        var result = await service.GetPriceCandlesAsync("ZZZ", start, end);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));

        var candle = data.First();
        Assert.Equal(1000m, candle.OpenPrice);
        Assert.Equal(1000m, candle.HighPrice);
        Assert.Equal(1000m, candle.LowPrice);
        Assert.Equal(1000m, candle.ClosePrice);
        Assert.Equal(dateCreated, candle.DateTime);
    }

    [Fact]
    public async Task GetPriceCandlesAsync_InvalidPeriod_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var logger = NullLogger<TokenPriceCandleQueryService>.Instance;
        var service = new TokenPriceCandleQueryService(db, logger);

        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(-1);

        var result = await service.GetPriceCandlesAsync("ZZZ", start, end);

        Assert.False(result.IsSuccess);
        Assert.Equal("Дата начала должна быть меньше даты окончания", result.Message);
    }

    [Fact]
    public async Task GetPriceCandlesAsync_EmptySymbol_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<TokenPriceCandleQueryService>.Instance;
        var service = new TokenPriceCandleQueryService(db, logger);

        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var result = await service.GetPriceCandlesAsync("", start, end);

        Assert.False(result.IsSuccess);
        Assert.Equal("Символ токена не может быть пустым", result.Message);
    }
}