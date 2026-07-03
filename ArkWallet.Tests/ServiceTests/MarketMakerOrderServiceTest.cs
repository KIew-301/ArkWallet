using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests;

public class MarketMakerOrderServiceTest
{
    [Fact]
    public async Task ExecuteMarketOrderAsync_BuyerRole_ExecutesAboveCurrentPrice()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", price: 100);
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(Result<OrderCreationData>.Ok(new OrderCreationData(true, new OrderDto(
                "1",
                OrderType.Buy,
                101,
                "ZZZ",
                15,
                120,
                OrderStatus.Active,
                DateTime.UtcNow
            ))));

        var logger = NullLogger<MarketMakerOrderService>.Instance;
        var service = new MarketMakerOrderService(db, mockOrderCreationService.Object, logger);

        var result = await service.ExecuteMarketOrderAsync((int)bot.TraderId, bot.Symbol);

        Assert.True(result.IsSuccess, result.Message);

        mockOrderCreationService.Verify(
            x => x.CreateOrderAsync(It.Is<CreateOrderCommand>(c => c.Price == 120)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteMarketOrderAsync_SellerRole_ExecutesBelowCurrentPrice()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", price: 100);
        await HelpMethods.GiveMoney(db, 102, 10000);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Seller, 50);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(Result<OrderCreationData>.Ok(new OrderCreationData(true, new OrderDto(
                "1",
                OrderType.Sell,
                101,
                "ZZZ",
                15,
                80,
                OrderStatus.Active,
                DateTime.UtcNow
            ))));

        var logger = NullLogger<MarketMakerOrderService>.Instance;
        var service = new MarketMakerOrderService(db, mockOrderCreationService.Object, logger);

        var result = await service.ExecuteMarketOrderAsync((int)bot.TraderId, bot.Symbol);

        Assert.True(result.IsSuccess, result.Message);

        mockOrderCreationService.Verify(
            x => x.CreateOrderAsync(It.Is<CreateOrderCommand>(c => c.Price == 80)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteMarketOrderAsync_BotNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        var logger = NullLogger<MarketMakerOrderService>.Instance;
        var service = new MarketMakerOrderService(db, mockOrderCreationService.Object, logger);

        var result = await service.ExecuteMarketOrderAsync(101, "ZZZ");

        Assert.False(result.IsSuccess);
        Assert.Equal("Бот для трейдера 101 и токена ZZZ не найден", result.Message);
    }

    [Fact]
    public async Task ExecuteMarketOrderAsync_TokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        var logger = NullLogger<MarketMakerOrderService>.Instance;
        var service = new MarketMakerOrderService(db, mockOrderCreationService.Object, logger);

        var result = await service.ExecuteMarketOrderAsync((int)bot.TraderId, bot.Symbol);

        Assert.False(result.IsSuccess);
        Assert.Equal("Токен ZZZ не найден", result.Message);
    }

    [Fact]
    public async Task ExecuteMarketOrderAsync_QuantityIsCalculatedCorrectly()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", price: 100);
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(Result<OrderCreationData>.Ok(new OrderCreationData(true, new OrderDto(
                "1",
                OrderType.Buy,
                101,
                "ZZZ",
                15,
                120,
                OrderStatus.Active,
                DateTime.UtcNow
            ))));

        var logger = NullLogger<MarketMakerOrderService>.Instance;
        var service = new MarketMakerOrderService(db, mockOrderCreationService.Object, logger);

        await service.ExecuteMarketOrderAsync((int)bot.TraderId, bot.Symbol);

        mockOrderCreationService.Verify(
            x => x.CreateOrderAsync(It.Is<CreateOrderCommand>(c => c.Quantity == 15)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteMarketOrderAsync_OrderCreationFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", price: 100);
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(Result<OrderCreationData>.Fail("Order creation failed"));

        var logger = NullLogger<MarketMakerOrderService>.Instance;
        var service = new MarketMakerOrderService(db, mockOrderCreationService.Object, logger);

        var result = await service.ExecuteMarketOrderAsync((int)bot.TraderId, bot.Symbol);

        Assert.False(result.IsSuccess);
        Assert.Equal("Не удалось создать ордер: Order creation failed", result.Message);
    }
}