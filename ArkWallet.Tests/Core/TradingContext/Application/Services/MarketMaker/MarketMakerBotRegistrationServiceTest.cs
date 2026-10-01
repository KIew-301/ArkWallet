using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.MarketMaker;

public class MarketMakerBotRegistrationServiceTest
{
    [Fact]
    public async Task RegisterBotAsync_ValidData_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, logger);

        var result = await service.RegisterBotAsync("ZZZ", BotRole.Buyer, 50);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.TraderId > 0);
        Assert.True(data.BotId > 0);
    }

    [Fact]
    public async Task RegisterBotAsync_InvalidSymbol_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, logger);

        var result = await service.RegisterBotAsync("", BotRole.Buyer, 50);

        Assert.False(result.IsSuccess);
        Assert.Equal("Символ токена не может быть пустым", result.Message);
    }

    [Fact]
    public async Task RegisterBotAsync_InvalidInitialPower_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, logger);

        var result = await service.RegisterBotAsync("ZZZ", BotRole.Buyer, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Начальная мощность должна быть больше нуля", result.Message);
    }
}