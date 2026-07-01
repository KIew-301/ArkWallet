using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.HelpTools;

internal class HelpMethods
{
    public static async Task<Result> RegisterTrader(ArkWalletDbContext db, long telegramId, string name = "User")
    {
        var service = new TraderRegistrationService(db);
        return await service.RegisterTraderAsync(telegramId, name);
    }

    public static async Task<Result> GiveMoney(ArkWalletDbContext db, long telegramId, decimal amount)
    {
        var service = new TraderBalanceUpdatingService(db);
        return await service.AddToBalanceAsync(telegramId, amount);
    }

    public static async Task<Result<TokenCreationData>> CreateToken(
        ArkWalletDbContext db,
        string symbol,
        string name = "Token",
        CharacterRarity rarity = CharacterRarity.FourStar,
        int totalSupply = 1000,
        decimal price = 10000,
        bool isActive = true,
        string imageUrl = "image.zzz",
        string iconUrl = "icon.zzz")
    {
        var service = new TokenCreationService(db);
        return await service.CreateTokenAsync(new CreateTokenCommand(
            symbol,
            name,
            rarity,
            price,
            totalSupply,
            isActive,
            imageUrl,
            iconUrl
        ));
    }

    public static async Task<Result> AddPortfolio(ArkWalletDbContext db, long traderId, string symbol, int quantity)
    {
        var service = new PortfolioUpdatingService(db);
        return await service.CreateOrUpdatePortfolioAsync(traderId, symbol, quantity);
    }

    public static async Task GiveToken(ArkWalletDbContext db, long traderId, string symbol, int quantity)
    {
        var item = await db.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
        if (item != null) item.BuyTokens(quantity, item.AverageBuyPrice);
    }

    public static async Task RemoveToken(ArkWalletDbContext db, long traderId, string symbol, int quantity)
    {
        var item = await db.PortfolioItems.FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == symbol);
        if (item != null) item.RemoveTokens(quantity, item.AverageBuyPrice);
    }

    public static async Task<Result<OrderCreationData>> PlaceOrder(ArkWalletDbContext db, long traderId, string direction,
        string symbol, int quantity, decimal price)
    {
        var engine = new TradingEngine();
        var mockValidator = new Mock<IOrderCreationFullValidationService>();
        mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<CreateOrderCommand>()))
            .ReturnsAsync(new ValidationResult(true));
        var mockTaskDispatcher = new Mock<ITaskDispatcher>();
        mockTaskDispatcher
            .Setup(x => x.SendTaskAsync(It.IsAny<string>(), It.IsAny<object>()));
        var service = new OrderCreationService(db, engine, mockValidator.Object, mockTaskDispatcher.Object);
        return await service.CreateOrderAsync(new CreateOrderCommand(traderId, direction, symbol, quantity, price));
    }

    public static async Task<Result> SaveBalanceSnapshot(
        ArkWalletDbContext db,
        long traderTelegramId,
        decimal totalBalance,
        decimal mainBalance,
        decimal longOrderReserve,
        decimal shortOrderReserve,
        decimal balanceInTokens,
        DateTime snapshotDateTime)
    {
        var logger = NullLogger<BalanceSavingService>.Instance;
        var service = new BalanceSavingService(db, logger);
        return await service.SaveBalanceToDatabase(
            traderTelegramId, totalBalance, mainBalance,
            longOrderReserve, shortOrderReserve, balanceInTokens,
            snapshotDateTime);
    }

    public static async Task<Result> CancelOrder(ArkWalletDbContext db, long traderId, string orderId)
    {
        var service = new OrderCancelService(db);
        return await service.CancelOrderAsync(traderId, orderId);
    }

    public static async Task<Result> CancelOrder(ArkWalletDbContext db, long traderId, Result<OrderCreationData> result)
    {
        var service = new OrderCancelService(db);
        if (!result.TryGetData(out var data))
            return Result.Fail("Отсутствует созданный ордер");
        return await service.CancelOrderAsync(traderId, data.Order.Id);
    }

    public static async Task<Result> CancelAllOrders(ArkWalletDbContext db, long traderId)
    {
        var service = new OrderCancelService(db);
        return await service.CancelAllOrderAsync(traderId);
    }

    public static async Task<Result<BalanceSnapshotData>> TakeBalanceSnapshot(ArkWalletDbContext db, long traderId)
    {
        var logger = NullLogger<BalanceSnapshotService>.Instance;
        var service = new BalanceSnapshotService(db, logger);
        return await service.TakeTotalTraderBalanceSnapshot(traderId);
    }

    public static async Task<Trader> GetTrader(ArkWalletDbContext db, long telegramId) =>
        await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == telegramId);

    public static async Task<PortfolioItem> GetPortfolio(ArkWalletDbContext db, long traderId, string symbol = "ZZZ") =>
        await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterToken.Symbol == symbol);

    public static async Task<TradeOrder[]> GetTraderOrders(ArkWalletDbContext db, long traderId, string symbol = "ZZZ", OrderStatus status = OrderStatus.Active) =>
        await db.TradeOrders
            .Include(o => o.CharacterToken)
            .Where(o => o.TraderTelegramId == traderId && o.CharacterToken.Symbol == symbol && o.Status == status)
            .ToArrayAsync();

    public static async Task<BalanceSnapshot[]> GetBalanceHistory(ArkWalletDbContext db, long traderId) =>
        await db.BalanceSnapshots
            .Where(s => s.TraderId == traderId)
            .ToArrayAsync();

    public static async Task CreatePriceCandle(ArkWalletDbContext db, string symbol, decimal price, DateTime timestamp)
    {
        var candle = PriceCandle.CreateNew(symbol, price, timestamp);
        await db.PriceCandles.AddAsync(candle);
        await db.SaveChangesAsync();
    }
}
