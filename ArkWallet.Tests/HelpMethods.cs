using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests;

internal class HelpMethods
{
    public static async Task<RegistrationResult> RegisterTrader(ArkWalletDbContext db, long telegramId, string name = "User")
    {
        var service = new TraderRegistrationService(db);
        return await service.RegisterTraderAsync(telegramId, name);
    }

    public static async Task<TokenCreationResult> CreateToken(ArkWalletDbContext db, string symbol, string name = "Token",
        CharacterRarity rarity = CharacterRarity.FourStar, int totalSupply = 1000, int price = 10000, bool isActive = true)
    {
        var service = new TokenCreationService(db);
        return await service.CreateTokenAsync(new CreateTokenCommand(symbol, name, rarity, totalSupply, price, isActive));
    }

    public static async Task<PortfolioUpdatingResult> AddPortfolio(ArkWalletDbContext db, long traderId, string symbol, int quantity)
    {
        var service = new PortfolioUpdatingService(db);
        return await service.CreateOrUpdatePortfolioAsync(traderId, symbol, quantity);
    }

    public static async Task<OrderCreationResult> PlaceOrder(ArkWalletDbContext db, long traderId, string direction,
        string symbol, int quantity, decimal price)
    {
        var engine = new TradingEngine();
        var service = new OrderCreationService(db, engine, null);
        return await service.CreateOrderAsync(new CreateOrderCommand(traderId, direction, symbol, quantity, price));
    }

    public static async Task<CancelOrderResult> CancelOrder(ArkWalletDbContext db, long traderId, string orderId)
    {
        var service = new OrderCancelService(db);
        return await service.CancelOrderAsync(traderId, orderId);
    }

    public static async Task<Trader> GetTrader(ArkWalletDbContext db, long telegramId) =>
        await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == telegramId);

    public static async Task<PortfolioItem> GetPortfolio(ArkWalletDbContext db, long traderId, string symbol = "ZZZ") =>
        await db.PortfolioItems
            .Include(p => p.CharacterToken)
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterToken.Symbol == symbol);
}
