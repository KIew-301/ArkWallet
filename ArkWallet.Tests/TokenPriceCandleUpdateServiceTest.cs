using ArkWallet.Application.Services.CharacterTokenServices;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
namespace ArkWallet.Tests;

public class TokenPriceCandleUpdateServiceTest
{
    [Fact]
    public async Task CreateCandle_FirstCandle_ReturnsSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<TokenPriceCandleUpdateService>.Instance;
        var timeProvider = new TestTimeProvider();
        var tokenPriceCandleUpdateService = new TokenPriceCandleUpdateService(db, timeProvider, logger);

        await HelpMethods.CreateToken(db, "ZZZ");
        var result = await tokenPriceCandleUpdateService.UpdateTokenPriceCandleAsync("ZZZ", 1000m);

        var candles = db.PriceCandles.Where(c => c.CharacterTokenId == "ZZZ").ToArray();

        Assert.True(result.IsSuccess, result.message);
        Assert.Single(candles);
        Assert.Equal(timeProvider.DateTimeOffsetNow, candles[0].Timestamp);
        Assert.Equal(1000m, candles[0].OpenPrice);
        Assert.Equal(1000m, candles[0].LowPrice);
        Assert.Equal(1000m, candles[0].HighPrice);
        Assert.Equal(1000m, candles[0].ClosePrice);
    }

    [Fact]
    public async Task CreateCandle_TokenNotFound_ReturnsFail()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<TokenPriceCandleUpdateService>.Instance;
        var timeProvider = new TestTimeProvider();
        var tokenPriceCandleUpdateService = new TokenPriceCandleUpdateService(db, timeProvider, logger);

        var result = await tokenPriceCandleUpdateService.UpdateTokenPriceCandleAsync("ZZZ", 1000m);

        Assert.False(result.IsSuccess);
        Assert.Equal("Токен не найден", result.message);
    }

    [Fact]
    public async Task CreateCandles_ThreeMinutes_PriceChangesPeriodically_ReturnsSuccess()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<TokenPriceCandleUpdateService>.Instance;
        var timeProvider = new TestTimeProvider();
        var tokenPriceCandleUpdateService = new TokenPriceCandleUpdateService(db, timeProvider, logger);
        var priceArray = new[] { 1000m, 1250m, 950m, 1050m, 950m, 920m, 1000m, 1500m, 1800m };

        await HelpMethods.CreateToken(db, "ZZZ");

        foreach(var price in priceArray)
        {
            await tokenPriceCandleUpdateService.UpdateTokenPriceCandleAsync("ZZZ", price);
            timeProvider.SkipInSeconds(20);
        }

        var candles = db.PriceCandles.Where(c => c.CharacterTokenId == "ZZZ").OrderBy(c => c.Timestamp).ToArray();

        Assert.Equal(3, candles.Length);

        Assert.Equal(timeProvider.DateTimeOffsetNow.AddSeconds(-180), candles[0].Timestamp);
        Assert.Equal(timeProvider.DateTimeOffsetNow.AddSeconds(-120), candles[1].Timestamp);
        Assert.Equal(timeProvider.DateTimeOffsetNow.AddSeconds(-60), candles[2].Timestamp);

        Assert.Equal(1000m, candles[0].OpenPrice);
        Assert.Equal(950m, candles[0].LowPrice);
        Assert.Equal(1250m, candles[0].HighPrice);
        Assert.Equal(950m, candles[0].ClosePrice);

        Assert.Equal(950m, candles[1].OpenPrice);
        Assert.Equal(920m, candles[1].LowPrice);
        Assert.Equal(1050m, candles[1].HighPrice);
        Assert.Equal(920m, candles[1].ClosePrice);

        Assert.Equal(920m, candles[2].OpenPrice);
        Assert.Equal(920m, candles[2].LowPrice);
        Assert.Equal(1800m, candles[2].HighPrice);
        Assert.Equal(1800m, candles[2].ClosePrice);
    }
}
