using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderCreationBatchServiceTest
{
    private static ArkWalletDbContext CreateDb()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        return db;
    }

    private static OrderCreationService CreateService(ArkWalletDbContext db)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db,
            TimeProvider.System,
            NullLogger<TokenPriceCandleUpdateService>.Instance);

        return new OrderCreationService(
            db,
            new TradingEngine(),
            new OrderValidationService(db),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            new Mock<ITaskDispatcher>().Object,
            NullLogger<OrderCreationService>.Instance);
    }

    [Fact]
    public async Task CreateOrdersAsync_EmptyCommands_ReturnsOkWithEmptyList()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.CreateOrdersAsync(new List<CreateOrderCommand>());

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task CreateOrdersAsync_MultipleBuyOrdersSameSymbol_ReturnsSuccess()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10000);

        var service = CreateService(db);
        var commands = new List<CreateOrderCommand>
        {
            new(101, "купить", "ZZZ", 5, 100),
            new(101, "купить", "ZZZ", 5, 90)
        };

        var result = await service.CreateOrdersAsync(commands);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task CreateOrdersAsync_TwoSymbols_TwoGroups_ReturnsSuccess()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.CreateToken(db, "AAA");
        await HelpMethods.GiveMoney(db, 101, 10000);

        var service = CreateService(db);
        var commands = new List<CreateOrderCommand>
        {
            new(101, "купить", "ZZZ", 5, 100),
            new(101, "купить", "AAA", 5, 50)
        };

        var result = await service.CreateOrdersAsync(commands);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task CreateOrdersAsync_InvalidPrice_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = CreateService(db);
        var result = await service.CreateOrdersAsync(new[]
        {
            new CreateOrderCommand(101, "купить", "ZZZ", 5, 0)
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("Цена", result.Message);
    }

    [Fact]
    public async Task CreateOrdersAsync_BuyInsufficientFunds_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = CreateService(db);
        var result = await service.CreateOrdersAsync(new[]
        {
            new CreateOrderCommand(101, "купить", "ZZZ", 5, 1000)
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("Insufficient balance", result.Message);
    }

    [Fact]
    public async Task CreateOrdersAsync_SellInsufficientTokens_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 3);

        var service = CreateService(db);
        var commands = new[]
        {
            new CreateOrderCommand(101, "продать", "ZZZ", 2, 100),
            new CreateOrderCommand(101, "продать", "ZZZ", 2, 90)
        };

        var result = await service.CreateOrdersAsync(commands);

        Assert.False(result.IsSuccess);
        Assert.Contains("Not enough tokens in portfolio", result.Message);
    }

    [Fact]
    public async Task CreateOrdersAsync_SellWithSufficientTokens_ReturnsSuccess()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);

        var service = CreateService(db);
        var commands = new List<CreateOrderCommand>
        {
            new(101, "продать", "ZZZ", 2, 100),
            new(101, "продать", "ZZZ", 2, 90)
        };

        var result = await service.CreateOrdersAsync(commands);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task CreateOrderAsync_SellWithoutOwnedToken_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = CreateService(db);
        var result = await service.CreateOrderAsync(new CreateOrderCommand(101, "продать", "ZZZ", 2, 100));

        Assert.False(result.IsSuccess);
        Assert.Contains("No portfolio item", result.Message);
    }

    [Fact]
    public async Task CreateOrdersAsync_TokenPriceUpdateFails_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.GiveMoney(db, 101, 10000);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);
        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 90);

        var candleService = new Mock<ITokenPriceCandleUpdateService>();
        candleService
            .Setup(x => x.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result.Fail("candle failure"));

        var service = new OrderCreationService(
            db,
            new TradingEngine(),
            new OrderValidationService(db),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleService.Object)),
            new Mock<ITaskDispatcher>().Object,
            NullLogger<OrderCreationService>.Instance);

        var result = await service.CreateOrdersAsync(new[]
        {
            new CreateOrderCommand(101, "купить", "ZZZ", 5, 100)
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("candle failure", result.Message);
    }

    [Fact]
    public async Task CreateOrderAsync_InvalidQuantity_ReturnsFail()
    {
        using var db = CreateDb();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = CreateService(db);
        var result = await service.CreateOrderAsync(new CreateOrderCommand(101, "купить", "ZZZ", -1, 100));

        Assert.False(result.IsSuccess);
        Assert.Contains("Количество", result.Message);
    }
}
