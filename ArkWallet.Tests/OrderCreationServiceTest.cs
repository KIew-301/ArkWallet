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
    public record TestOrder(long TraderId, string Direction, string Symbol, int Quantity, decimal Price);
    public record TestPortfolio(long TraderId, string Symbol, int Quantity);

    [Fact]
    public async Task ProcessOrdersAsync_MatchingTest_ReturnsSuccess()
    {
        var traderRecord1 = new TestTrader(101, "First");
        var traderRecord2 = new TestTrader(102, "Second");
        var tokenRecord = new TestToken("ZZZ", "Тест-валюта", CharacterRarity.FourStar, 1000, 10000, true);
        var portfolioRecord = new TestPortfolio(traderRecord2.TelegramId, tokenRecord.Symbol, 10);
        var orderRecord1 = new TestOrder(traderRecord1.TelegramId, "купить", tokenRecord.Symbol, 5, 100);
        var orderRecord2 = new TestOrder(traderRecord2.TelegramId, "продать", tokenRecord.Symbol, 5, 100);
        var startBalance = 1000;

        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var traderRegistrationService = new TraderRegistrationService(db);
        var tokenCreationService = new TokenCreationService(db);
        var portfolioUpdatingService = new PortfolioUpdatingService(db);
        var tradingEngine = new TradingEngine();
        var tradingService = new OrderCreationService(db, tradingEngine, null);

        await traderRegistrationService.RegisterTraderAsync(traderRecord1.TelegramId, traderRecord1.Name);
        await traderRegistrationService.RegisterTraderAsync(traderRecord2.TelegramId, traderRecord2.Name);
        await tokenCreationService.CreateTokenAsync(new CreateTokenCommand(
            tokenRecord.Symbol, tokenRecord.Name, tokenRecord.Rarity, tokenRecord.TotalSupply, tokenRecord.CurrentPrice, tokenRecord.IsActive));
        await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(
            portfolioRecord.TraderId, portfolioRecord.Symbol, portfolioRecord.Quantity);

        var result1 = await tradingService.CreateOrderAsync(new CreateOrderCommand(
            orderRecord1.TraderId, orderRecord1.Direction, orderRecord1.Symbol, orderRecord1.Quantity, orderRecord1.Price));

        var result2 = await tradingService.CreateOrderAsync(new CreateOrderCommand(
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
        Assert.Equal(portfolioRecord.Quantity - orderRecord2.Quantity, porfolio2.Quantity);
        Assert.Equal(startBalance - orderRecord1.Quantity * orderRecord1.Price, trader1.Balance);
        Assert.Equal(startBalance + orderRecord2.Quantity * orderRecord2.Price, trader2.Balance);
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

        var order = new CreateOrderCommand(101, "купить", "ZZZ", 5, 100);

        // Запускаем оба процесса одновременно
        var result = await tradingService.CreateOrderAsync(order);

        // Проверяем результаты
        Assert.True(result.IsSuccess, $"Order failed: {result.ErrorMessage}");
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
        await portfolioUpdatingService.CreateOrUpdatePortfolioAsync(101, "ZZZ", 10);

        var order1 = new CreateOrderCommand(101, "продать", "ZZZ", 5, 100);

        // Запускаем оба процесса одновременно
        var result = await tradingService.CreateOrderAsync(order1);

        // Проверяем результаты
        Assert.True(result.IsSuccess, $"Order failed: {result.ErrorMessage}");
    }
}
