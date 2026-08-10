using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Token;

public class TokenQueryServiceTest
{
    [Fact]
    public async Task GetAllActiveTokensAsync_WhenNoTokens_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetAllActiveTokensAsync_WhenTokensExist_ReturnsAllTokens()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);
        await HelpMethods.CreateToken(db, "YYY", "One", CharacterRarity.FourStar, 500, 50m);

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task GetAllActiveTokensAsync_WhenTokensExist_ReturnsCorrectTokenInfo()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 80m, DateTime.UtcNow);

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var token = data.First();
        Assert.Equal("ZZZ", token.TokenInfo.Symbol);
        Assert.Equal("Zero", token.TokenInfo.Name);
        Assert.Equal(100m, token.TokenInfo.CurrentPrice);
        Assert.Equal(25m, token.DailyChangePercent);
    }

    [Fact]
    public async Task GetAllActiveTokensAsync_WhenNoPriceHistory_ReturnsZeroPercent()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var token = data.First();
        Assert.Equal(100m, token.TokenInfo.CurrentPrice);
        Assert.Equal(0m, token.DailyChangePercent);
    }

    [Fact]
    public async Task GetAllActiveTokensAsync_WhenOnlyStaleCandle_ReturnsZeroPercent()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 80m, DateTime.UtcNow.AddDays(-2));

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var token = data.First();
        Assert.Equal(0m, token.DailyChangePercent);
    }

    [Theory]
    [InlineData("NONEXISTENT", false)]
    [InlineData("ZZZ", true)]
    [InlineData("zzz", false)]
    public async Task GetTokenInfoAsync_VariousScenarios(string symbol, bool expectedSuccess)
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        if (expectedSuccess)
            await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        var result = await service.GetTokenInfoAsync(symbol);

        Assert.Equal(expectedSuccess, result.IsSuccess);

        if (expectedSuccess && result.TryGetData(out var data))
        {
            Assert.Equal("ZZZ", data.Symbol);
            Assert.Equal("Zero", data.Name);
            Assert.Equal(100m, data.CurrentPrice);
        }
    }
}