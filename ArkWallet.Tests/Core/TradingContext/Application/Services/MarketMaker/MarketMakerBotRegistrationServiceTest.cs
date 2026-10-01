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

    [Fact]
    public async Task CreateDedicatedTraderAsync_CreatesBotTrader_ReturnId()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, logger);

        var result = await service.CreateDedicatedTraderAsync("TKN01", BotRole.Buyer);

        Assert.True(result.TryGetData(out var traderId));
        Assert.True(traderId > 0);
        var trader = db.Traders.Find(traderId);
        Assert.NotNull(trader);
        Assert.True(trader.IsBot);
        Assert.Contains("TKN01", trader.Username);
        Assert.Contains("Buyer", trader.Username);
    }

    [Fact]
    public async Task CreateDedicatedTraderAsync_BuyerAndSeller_GetSeparateTraders()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var service = new MarketMakerBotRegistrationService(db, logger);

        var buyerResult = await service.CreateDedicatedTraderAsync("TKN01", BotRole.Buyer);
        var sellerResult = await service.CreateDedicatedTraderAsync("TKN01", BotRole.Seller);

        Assert.True(buyerResult.TryGetData(out var buyerId));
        Assert.True(sellerResult.TryGetData(out var sellerId));
        Assert.NotEqual(buyerId, sellerId);
        Assert.True(buyerId > 0);
        Assert.True(sellerId > 0);

        var buyersTrader = db.Traders.Find(buyerId);
        var sellersTrader = db.Traders.Find(sellerId);

        Assert.NotNull(buyersTrader);
        Assert.NotNull(sellersTrader);
        Assert.True(buyersTrader.IsBot);
        Assert.True(sellersTrader.IsBot);
    }
}