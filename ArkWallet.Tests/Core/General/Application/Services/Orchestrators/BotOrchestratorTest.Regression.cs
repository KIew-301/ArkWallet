using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ArkWallet.Tests.Core.General.Application.Services.Orchestrators;

public partial class BotOrchestratorTest
{
    // ═══════ Regression: balance defaults & ReplenishBotBalancesAsync ═══════

    [Fact]
    public void MarketMakerBot_DefaultBalance_IsOneThousandMillion()
    {
        Assert.Equal(1_000_000_000m, MarketMakerBot.DefaultBalance);
    }

    /// <summary>
    /// Баланс бота не может быть причиной отказа от выставления: ордера резервируют средства,
    /// поэтому перед размещением баланс обязан быть приведён к норме. Раньше бот с балансом 0
    /// просто пропускался, и сетка молча не выставлялась до пополнения по отдельному расписанию.
    /// </summary>
    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_BuyerWithZeroBalance_ReplenishesToDefaultBeforePlacing()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_ZB", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_ZB", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_ZB", 100m, DateTime.UtcNow);

        var buyerId = 15101L;
        await HelpMethods.RegisterTrader(db, buyerId);
        await SeedPortfolioAsync(db, buyerId, "TKN_ZB");
        var buyerTrader = await db.Traders.FirstAsync(t => t.TelegramId == buyerId);
        buyerTrader.Balance = 0m;
        await db.SaveChangesAsync();

        var bot = MarketMakerBotRecord.Create(buyerId, "TKN_ZB", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Buyer);

        Assert.True(result.IsSuccess);
        Assert.Equal(MarketMakerBot.DefaultBalance, buyerTrader.Balance);
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Once);
    }

    /// <summary>
    /// Приведение баланса к норме работает в обе стороны — не только пополнение вверх,
    /// но и сброс завышенного значения до DefaultBalance.
    /// </summary>
    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_SellerWithStaleExcessBalance_BringsBalanceDownToDefault()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_SB", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_SB", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_SB", 100m, DateTime.UtcNow);

        var sellerId = 15201L;
        await HelpMethods.RegisterTrader(db, sellerId);
        await SeedPortfolioAsync(db, sellerId, "TKN_SB");
        var sellerTrader = await db.Traders.FirstAsync(t => t.TelegramId == sellerId);
        sellerTrader.Balance = 5_000_000_000m;
        await db.SaveChangesAsync();

        var bot = MarketMakerBotRecord.Create(sellerId, "TKN_SB", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller);

        Assert.True(result.IsSuccess);
        Assert.Equal(MarketMakerBot.DefaultBalance, sellerTrader.Balance);
    }

    /// <summary>
    /// Ребалансировка баланса применяется абсолютно ко всем ботам вне зависимости от того,
    /// находится ли их баланс выше или ниже нормы — итог всегда равен DefaultBalance.
    /// </summary>
    [Fact]
    public async Task UpdateAllBotsBalancesAsync_BotBalanceAboveOrBelowDefault_SetsExactDefault()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_AB", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_AB", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_AB", 100m, DateTime.UtcNow);

        var botBelow = 15301L;
        var botAbove = 15302L;
        await HelpMethods.RegisterTrader(db, botBelow);
        await HelpMethods.RegisterTrader(db, botAbove);

        var belowTrader = await db.Traders.FirstAsync(t => t.TelegramId == botBelow);
        belowTrader.Balance = 0m;
        await db.SaveChangesAsync();

        var aboveTrader = await db.Traders.FirstAsync(t => t.TelegramId == botAbove);
        aboveTrader.Balance = 7_777_777_777m;
        await db.SaveChangesAsync();

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(botBelow, "TKN_AB", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(botAbove, "TKN_AB", BotRole.Buyer, 50m));
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateAllBotsBalancesAsync();

        Assert.True(result.IsSuccess);
        var freshBelow = await db.Traders.FirstAsync(t => t.Id == botBelow);
        var freshAbove = await db.Traders.FirstAsync(t => t.Id == botAbove);
        Assert.Equal(MarketMakerBot.DefaultBalance, freshBelow.Balance);
        Assert.Equal(MarketMakerBot.DefaultBalance, freshAbove.Balance);
    }
}
