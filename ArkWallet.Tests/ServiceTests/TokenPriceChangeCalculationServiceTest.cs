using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests;

public class TokenPriceChangeCalculationServiceTest
{
    [Fact]
    public async Task TakeTokenPriceChangesAsync_TokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<TokenPriceChangeCalculationService>.Instance;
        var service = new TokenPriceChangeCalculationService(db, logger, timeProvider);

        var result = await service.TakeTokenPriceChangesAsync("UNKNOWN", 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Токен с идентификатором UNKNOWN не найден", result.Message);
    }

    [Fact]
    public async Task TakeTokenPriceChangesAsync_InvalidPeriod_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<TokenPriceChangeCalculationService>.Instance;
        var service = new TokenPriceChangeCalculationService(db, logger, timeProvider);

        var result = await service.TakeTokenPriceChangesAsync("ZZZ", 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Минимальный период для расчёта: 1 день", result.Message);
    }

    [Fact]
    public async Task TakeTokenPriceChangesAsync_NoPriceHistory_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<TokenPriceChangeCalculationService>.Instance;
        var service = new TokenPriceChangeCalculationService(db, logger, timeProvider);

        var result = await service.TakeTokenPriceChangesAsync("ZZZ", 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Истории цены токена не существует", result.Message);
    }

    [Fact]
    public async Task TakeTokenPriceChangesAsync_SinglePriceHistory_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<TokenPriceChangeCalculationService>.Instance;
        var service = new TokenPriceChangeCalculationService(db, logger, timeProvider);

        await HelpMethods.CreateToken(db, "ZZZ", price: 1500m);
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1000m, timeProvider.GetUtcNow().DateTime.AddDays(-1));

        var result = await service.TakeTokenPriceChangesAsync("ZZZ", 1);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        Assert.Equal(1500m, data.CurrentPrice);
        Assert.Equal(1000m, data.PreviousPrice);
        Assert.Equal(500m, data.ChangeAbsolute);
        Assert.Equal(50m, data.ChangePercent);
    }

    [Fact]
    public async Task TakeTokenPriceChangesAsync_TwoPriceHistories_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<TokenPriceChangeCalculationService>.Instance;
        var service = new TokenPriceChangeCalculationService(db, logger, timeProvider);

        await HelpMethods.CreateToken(db, "ZZZ", price: 2000m);
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1000m, timeProvider.Now.Date.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1200m, timeProvider.Now.Date.AddDays(-1));

        var result = await service.TakeTokenPriceChangesAsync("ZZZ", 2);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        Assert.Equal(2000m, data.CurrentPrice);
        Assert.Equal(1000m, data.PreviousPrice);
        Assert.Equal(1000m, data.ChangeAbsolute);
        Assert.Equal(100m, data.ChangePercent);
    }

    [Theory]
    [InlineData(1, 2000, 1500)]
    [InlineData(2, 2000, 1200)]
    [InlineData(3, 2000, 1200)]
    [InlineData(7, 2000, 1000)]
    public async Task TakeTokenPriceChangesAsync_DifferentPeriods_ReturnsCorrectData(
        int period, decimal currentPrice, decimal previousPrice)
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<TokenPriceChangeCalculationService>.Instance;
        var service = new TokenPriceChangeCalculationService(db, logger, timeProvider);

        await HelpMethods.CreateToken(db, "ZZZ", price: currentPrice);
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1000m, timeProvider.Now.Date.AddDays(-7));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1100m, timeProvider.Now.Date.AddDays(-5));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1200m, timeProvider.Now.Date.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 1500m, timeProvider.Now.Date.AddDays(-1));

        var result = await service.TakeTokenPriceChangesAsync("ZZZ", period);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var changeAbsolute = currentPrice - previousPrice;
        var changePercent = previousPrice != 0 ? changeAbsolute / previousPrice * 100m : 0m;

        Assert.Equal(currentPrice, data.CurrentPrice);
        Assert.Equal(previousPrice, data.PreviousPrice);
        Assert.Equal(changeAbsolute, data.ChangeAbsolute, precision: 2);
        Assert.Equal(changePercent, data.ChangePercent, precision: 2);
    }
}