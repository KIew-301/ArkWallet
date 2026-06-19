using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests;

public class PortfolioUpdatingServiceTest
{
    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_CreateNewPorfolio_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var portfolioUpdatingService = new PortfolioUpdatingService(db);
        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);

        // Подготовка данных
        await traderRegistrationService.RegisterTraderAsync(101, "User");
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true));
        var result = await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(101, "ZZZ", 100);

        var portfolio = await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == 101 && p.CharacterToken.Symbol == "ZZZ");

        Assert.True(result.IsSuccess);
        Assert.NotNull(portfolio);
        Assert.Equal(100, portfolio.Quantity);
    }

    [Fact]
    public async Task CreateOrUpdatePortfolioAsync_UpdatePorfolio_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var portfolioUpdatingService = new PortfolioUpdatingService(db);
        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);

        // Подготовка данных
        await traderRegistrationService.RegisterTraderAsync(101, "User");
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true));
        await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(101, "ZZZ", 100);
        var result = await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(101, "ZZZ", 50);

        var portfolio = await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == 101 && p.CharacterToken.Symbol == "ZZZ");

        Assert.True(result.IsSuccess);
        Assert.NotNull(portfolio);
        Assert.Equal(150, portfolio.Quantity);
    }
}
