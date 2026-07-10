using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.MarketMaker;

public class MarketMakerBotRegistrationServiceTest
{
    [Fact]
    public async Task RegisterBotAsync_ValidData_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockRegistrationService = new Mock<ITraderRegistrationService>();
        mockRegistrationService
            .Setup(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok());

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, mockRegistrationService.Object, logger);

        var result = await service.RegisterBotAsync(101, "ZZZ", BotRole.Buyer, 50);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(101, data.TraderId);
    }

    [Fact]
    public async Task RegisterBotAsync_InvalidSymbol_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockRegistrationService = new Mock<ITraderRegistrationService>();
        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, mockRegistrationService.Object, logger);

        var result = await service.RegisterBotAsync(101, "", BotRole.Buyer, 50);

        Assert.False(result.IsSuccess);
        Assert.Equal("Символ токена не может быть пустым", result.Message);
    }

    [Fact]
    public async Task RegisterBotAsync_InvalidInitialPower_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockRegistrationService = new Mock<ITraderRegistrationService>();
        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, mockRegistrationService.Object, logger);

        var result = await service.RegisterBotAsync(101, "ZZZ", BotRole.Buyer, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Начальная мощность должна быть больше нуля", result.Message);
    }

    [Fact]
    public async Task RegisterBotAsync_WhenRegistrationFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockRegistrationService = new Mock<ITraderRegistrationService>();
        mockRegistrationService
            .Setup(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail("Registration error"));

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, mockRegistrationService.Object, logger);

        var result = await service.RegisterBotAsync(101, "ZZZ", BotRole.Buyer, 50);

        Assert.False(result.IsSuccess);
        Assert.Contains("Не удалось зарегистрировать трейдера", result.Message);
    }
}