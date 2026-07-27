using ArkWallet.Domain.Entities;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.MarketMaker;

public class MarketMakerBotQueryServiceTest
{
    [Fact]
    public async Task GetBotsBySymbolAsync_ReturnsMatchingBots()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        db.MarketMakerBots.AddRange(
            MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer),
            MarketMakerBot.Create(102, "ARK_001", BotRole.Seller),
            MarketMakerBot.Create(103, "ARK_002", BotRole.Buyer));
        await db.SaveChangesAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.GetBotsBySymbolAsync("ARK_001");

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var bots));
        Assert.Equal(2, bots.Count);
        Assert.All(bots, b => Assert.Equal("ARK_001", b.Symbol));
    }

    [Fact]
    public async Task GetBotsBySymbolAsync_EmptySymbol_ReturnsFailure()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.GetBotsBySymbolAsync("");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetBotsBySymbolAsync_NoBots_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.GetBotsBySymbolAsync("NONEXISTENT");

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var bots));
        Assert.Empty(bots);
    }

    [Fact]
    public async Task GetBotByIdAsync_ExistingBot_ReturnsBot()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);
        db.MarketMakerBots.Add(bot);
        await db.SaveChangesAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.GetBotByIdAsync(bot.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var found));
        Assert.Equal(bot.Id, found.Id);
        Assert.Equal("ARK_001", found.Symbol);
    }

    [Fact]
    public async Task GetBotByIdAsync_NonExistingBot_ReturnsFailure()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.GetBotByIdAsync(99999);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateBotAsync_UpdatesBasePower()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer, 50);
        db.MarketMakerBots.Add(bot);
        await db.SaveChangesAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.UpdateBotAsync(bot.Id, basePower: 200, role: null, isActive: null);

        Assert.True(result.IsSuccess);
        var updated = await db.MarketMakerBots.FindAsync(bot.Id);
        Assert.Equal(200, updated!.BasePower);
    }

    [Fact]
    public async Task UpdateBotAsync_UpdatesRole()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);
        db.MarketMakerBots.Add(bot);
        await db.SaveChangesAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.UpdateBotAsync(bot.Id, basePower: null, role: "Seller", isActive: null);

        Assert.True(result.IsSuccess);
        var updated = await db.MarketMakerBots.FindAsync(bot.Id);
        Assert.Equal(BotRole.Seller, updated!.Role);
    }

    [Fact]
    public async Task UpdateBotAsync_UpdatesIsActive()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var bot = MarketMakerBot.Create(101, "ARK_001", BotRole.Buyer);
        db.MarketMakerBots.Add(bot);
        await db.SaveChangesAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.UpdateBotAsync(bot.Id, basePower: null, role: null, isActive: false);

        Assert.True(result.IsSuccess);
        var updated = await db.MarketMakerBots.FindAsync(bot.Id);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task UpdateBotAsync_NonExistingBot_ReturnsFailure()
    {
        using var db = DbTest.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        var service = new MarketMakerBotQueryService(db, NullLogger<MarketMakerBotQueryService>.Instance);

        var result = await service.UpdateBotAsync(99999, basePower: 100, role: null, isActive: null);

        Assert.False(result.IsSuccess);
    }
}
