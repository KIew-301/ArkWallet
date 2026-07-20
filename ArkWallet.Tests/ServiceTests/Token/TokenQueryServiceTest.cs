using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Token;

public class TokenQueryServiceTest
{
    [Fact]
    public async Task GetAllActiveTokensAsync_WhenNoTokens_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockPriceChangeService = new Mock<ITokenPriceChangesCalculationService>();
        var logger = NullLogger<TokenQueryService>.Instance;
        var service = new TokenQueryService(db, mockPriceChangeService.Object, logger);

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

        var mockPriceChangeService = new Mock<ITokenPriceChangesCalculationService>();
        mockPriceChangeService
            .Setup(x => x.TakeTokenPriceChangesAsync(It.IsAny<string>(), 1))
            .ReturnsAsync(Result<TokenPriceChangesData>.Ok(new TokenPriceChangesData(100m, 90m, 10m, 11.11m)));

        var logger = NullLogger<TokenQueryService>.Instance;
        var service = new TokenQueryService(db, mockPriceChangeService.Object, logger);

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

        var mockPriceChangeService = new Mock<ITokenPriceChangesCalculationService>();
        mockPriceChangeService
            .Setup(x => x.TakeTokenPriceChangesAsync("ZZZ", 1))
            .ReturnsAsync(Result<TokenPriceChangesData>.Ok(new TokenPriceChangesData(100m, 90m, 10m, 11.11m)));

        var logger = NullLogger<TokenQueryService>.Instance;
        var service = new TokenQueryService(db, mockPriceChangeService.Object, logger);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var token = data.First();
        Assert.Equal("ZZZ", token.TokenInfo.Symbol);
        Assert.Equal("Zero", token.TokenInfo.Name);
        Assert.Equal(100m, token.TokenInfo.CurrentPrice);
        Assert.Equal(11.11m, token.DailyChangePercent);
    }

    [Fact]
    public async Task GetAllActiveTokensAsync_WhenPriceChangeFails_ReturnsZeroPercent()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);

        var mockPriceChangeService = new Mock<ITokenPriceChangesCalculationService>();
        mockPriceChangeService
            .Setup(x => x.TakeTokenPriceChangesAsync("ZZZ", 1))
            .ReturnsAsync(Result<TokenPriceChangesData>.Fail("Нет истории ценыЫ"));

        var logger = NullLogger<TokenQueryService>.Instance;
        var service = new TokenQueryService(db, mockPriceChangeService.Object, logger);

        var result = await service.GetAllActiveTokensAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var token = data.First();
        Assert.Equal(100m, token.TokenInfo.CurrentPrice);
        Assert.Equal(0m, token.DailyChangePercent);
    }

    [Fact]
    public async Task GetTokenInfoAsync_TokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockPriceChangeService = new Mock<ITokenPriceChangesCalculationService>();
        var logger = NullLogger<TokenQueryService>.Instance;
        var service = new TokenQueryService(db, mockPriceChangeService.Object, logger);

        var result = await service.GetTokenInfoAsync("NONEXISTENT");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetTokenInfoAsync_TokenExists_ReturnsTokenInfo()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FiveStar, 1000, 100m);

        var mockPriceChangeService = new Mock<ITokenPriceChangesCalculationService>();
        var logger = NullLogger<TokenQueryService>.Instance;
        var service = new TokenQueryService(db, mockPriceChangeService.Object, logger);

        var result = await service.GetTokenInfoAsync("ZZZ");

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("ZZZ", data.Symbol);
        Assert.Equal("Zero", data.Name);
        Assert.Equal(100m, data.CurrentPrice);
    }
}