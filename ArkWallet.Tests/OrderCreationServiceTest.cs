using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests;
public class OrderCreationServiceTest
{
    public record TestTrader(long TelegramId, string Name);
    public record TestToken(string Symbol, string Name, CharacterRarity Rarity, int TotalSupply, int CurrentPrice, bool IsActive);
    public record TestOrder(long TraderId, string Direction, string Symbol, int Price, int Quantity);
    public record TestPortfolio(long TraderId, string Symbol, int Quantity);

    [Fact]
    public async Task ProcessOrdersAsync_RaceConditionTest_ReturnsSuccess()
    {
        var traderRecord1 = new TestTrader(101, "First");
        var traderRecord2 = new TestTrader(102, "Second");
        var tokenRecord = new TestToken("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true);
        var portfolioRecord = new TestPortfolio(traderRecord2.TelegramId, tokenRecord.Symbol, 250);
        var orderRecord1 = new TestOrder(traderRecord1.TelegramId, "купить", tokenRecord.Symbol, 1000, 100);
        var orderRecord2 = new TestOrder(traderRecord2.TelegramId, "продать", tokenRecord.Symbol, 1000, 100);
        var startBalance = 1000;

        DbTest.InitTest(nameof(ProcessOrdersAsync_RaceConditionTest_ReturnsSuccess));
        using var db = DbTest.CreateHardDbContext(nameof(ProcessOrdersAsync_RaceConditionTest_ReturnsSuccess));

        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);
        var portfolioUpdatingService = new PortfolioUpdatingService(db);

        await traderRegistrationService.RegisterTraderAsync(traderRecord1.TelegramId, traderRecord1.Name);
        await traderRegistrationService.RegisterTraderAsync(traderRecord2.TelegramId, traderRecord2.Name);
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand(
            tokenRecord.Symbol, tokenRecord.Name, tokenRecord.Rarity, tokenRecord.TotalSupply, tokenRecord.CurrentPrice, tokenRecord.IsActive));
        await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
            portfolioRecord.TraderId, portfolioRecord.Symbol, portfolioRecord.Quantity);

        var task1 = Task.Run(async () =>
        {
            using var db1 = DbTest.CreateHardDbContext(nameof(ProcessOrdersAsync_RaceConditionTest_ReturnsSuccess));
            var tradingEngine = new TradingEngine();
            var tradingService = new OrderCreationService(db1, tradingEngine, null);
            return await tradingService.CreateOrderAsync(new CreateOrderCommand(
                orderRecord1.TraderId, orderRecord1.Direction, orderRecord1.Symbol, orderRecord1.Quantity, orderRecord1.Price));
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = DbTest.CreateHardDbContext(nameof(ProcessOrdersAsync_RaceConditionTest_ReturnsSuccess));
            var tradingEngine = new TradingEngine();
            var tradingService = new OrderCreationService(db2, tradingEngine, null);
            return await tradingService.CreateOrderAsync(new CreateOrderCommand(
                orderRecord2.TraderId, orderRecord2.Direction, orderRecord2.Symbol, orderRecord2.Quantity, orderRecord2.Price));
        });

        await Task.WhenAll(task1, task2);

        var trader1 = await db.Traders
            .FirstOrDefaultAsync(t => t.TelegramId == traderRecord1.TelegramId);
        var trader2 = await db.Traders
           .FirstOrDefaultAsync(t => t.TelegramId == traderRecord2.TelegramId);
        var porfolio1 = await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderRecord1.TelegramId);
        var porfolio2 = await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderRecord2.TelegramId);

        Assert.True(task1.Result.IsSuccess, $"Order1 failed: {task1.Result.ErrorMessage}");
        Assert.True(task2.Result.IsSuccess, $"Order2 failed: {task2.Result.ErrorMessage}");
        Assert.NotNull(trader1);
        Assert.NotNull(trader2);
        Assert.NotNull(porfolio1);
        Assert.NotNull(porfolio2);
        Assert.Equal(orderRecord1.Quantity, porfolio1.Quantity);
        Assert.Equal(orderRecord2.Quantity - orderRecord1.Quantity, porfolio2.Quantity);
        Assert.Equal(startBalance - orderRecord1.Quantity * orderRecord1.Price, trader1.Balance);
        Assert.Equal(startBalance - orderRecord2.Quantity * orderRecord2.Price, trader2.Balance);
    }

    [Fact]
    public async Task ProcessOrdersAsync_MatchingTest_ReturnsSuccess()
    {
        var traderRecord1 = new TestTrader(101, "First");
        var traderRecord2 = new TestTrader(102, "Second");
        var tokenRecord = new TestToken("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true);
        var portfolioRecord = new TestPortfolio(traderRecord2.TelegramId, tokenRecord.Symbol, 250);
        var orderRecord1 = new TestOrder(traderRecord1.TelegramId, "купить", tokenRecord.Symbol, 1000, 100);
        var orderRecord2 = new TestOrder(traderRecord2.TelegramId, "продать", tokenRecord.Symbol, 1000, 100);
        var startBalance = 1000;

        DbTest.InitTest(nameof(ProcessOrdersAsync_MatchingTest_ReturnsSuccess));
        using var db = DbTest.CreateHardDbContext(nameof(ProcessOrdersAsync_MatchingTest_ReturnsSuccess));

        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);
        var portfolioUpdatingService = new PortfolioUpdatingService(db);

        await traderRegistrationService.RegisterTraderAsync(traderRecord1.TelegramId, traderRecord1.Name);
        await traderRegistrationService.RegisterTraderAsync(traderRecord2.TelegramId, traderRecord2.Name);
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand(
            tokenRecord.Symbol, tokenRecord.Name, tokenRecord.Rarity, tokenRecord.TotalSupply, tokenRecord.CurrentPrice, tokenRecord.IsActive));
        await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
            portfolioRecord.TraderId, portfolioRecord.Symbol, portfolioRecord.Quantity);

        using var db1 = DbTest.CreateHardDbContext(nameof(ProcessOrdersAsync_MatchingTest_ReturnsSuccess));
        var tradingEngine1 = new TradingEngine();
        var tradingService1 = new OrderCreationService(db1, tradingEngine1, null);
        var result1 = await tradingService1.CreateOrderAsync(new CreateOrderCommand(
            orderRecord1.TraderId, orderRecord1.Direction, orderRecord1.Symbol, orderRecord1.Quantity, orderRecord1.Price));

        using var db2 = DbTest.CreateHardDbContext(nameof(ProcessOrdersAsync_MatchingTest_ReturnsSuccess));
        var tradingEngine2 = new TradingEngine();
        var tradingService2 = new OrderCreationService(db2, tradingEngine2, null);
        var result2 = await tradingService2.CreateOrderAsync(new CreateOrderCommand(
            orderRecord2.TraderId, orderRecord2.Direction, orderRecord2.Symbol, orderRecord2.Quantity, orderRecord2.Price));

        var trader1 = await db.Traders
            .FirstOrDefaultAsync(t => t.TelegramId == traderRecord1.TelegramId);
        var trader2 = await db.Traders
           .FirstOrDefaultAsync(t => t.TelegramId == traderRecord2.TelegramId);
        var porfolio1 = await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderRecord1.TelegramId);
        var porfolio2 = await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderRecord2.TelegramId);

        Assert.True(result1.IsSuccess, $"Order1 failed: {result1.ErrorMessage}");
        Assert.True(result2.IsSuccess, $"Order2 failed: {result2.ErrorMessage}");
        Assert.NotNull(trader1);
        Assert.NotNull(trader2);
        Assert.NotNull(porfolio1);
        Assert.NotNull(porfolio2);
        Assert.Equal(orderRecord1.Quantity, porfolio1.Quantity);
        Assert.Equal(orderRecord2.Quantity - orderRecord1.Quantity, porfolio2.Quantity);
        Assert.Equal(startBalance - orderRecord1.Quantity * orderRecord1.Price, trader1.Balance);
        Assert.Equal(startBalance - orderRecord2.Quantity * orderRecord2.Price, trader2.Balance);
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleLongOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var tradingEngine = new TradingEngine();
        var tradingService = new OrderCreationService(db, tradingEngine, null);
        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);

        // Подготовка данных
        await traderRegistrationService.RegisterTraderAsync(101, "User");
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true));

        var order = new CreateOrderCommand(101, "купить", "ZZZ", 1000, 100);

        // Запускаем оба процесса одновременно
        var result = await tradingService.CreateOrderAsync(order);

        // Проверяем результаты
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleShortOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var tradingEngine = new TradingEngine();
        var tradingService = new OrderCreationService(db, tradingEngine, null);
        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);
        var portfolioUpdatingService = new PortfolioUpdatingService(db);

        // Подготовка данных
        await traderRegistrationService.RegisterTraderAsync(101, "First");
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true));
        await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(101, "ZZZ", 100);

        var order1 = new CreateOrderCommand(101, "продать", "ZZZ", 1000, 100);

        // Запускаем оба процесса одновременно
        var result = await tradingService.CreateOrderAsync(order1);

        // Проверяем результаты
        Assert.True(result.IsSuccess);
    }
}
