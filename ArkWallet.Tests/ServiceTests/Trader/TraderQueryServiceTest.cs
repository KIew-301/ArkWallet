using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Trader;

public class TraderQueryServiceTest
{
    [Theory]
    [InlineData(999, false)]
    [InlineData(101, true)]
    public async Task GetTraderProfileAsync_VariousScenarios(long traderId, bool expectSuccess)
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        if (expectSuccess)
            await HelpMethods.RegisterTrader(db, traderId, "TestUser");

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderProfileAsync(traderId);

        Assert.Equal(expectSuccess, result.IsSuccess);

        if (expectSuccess && result.TryGetData(out var data))
            Assert.Equal("TestUser", data.Username);
    }

    [Fact]
    public async Task GetTraderProfileAsync_TraderWithNoName_ReturnsUnknown()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var trader = ArkWallet.Domain.Entities.Trader.Create(101, null);
        db.Traders.Add(trader);
        await db.SaveChangesAsync();

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderProfileAsync(101);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("Unknown", data.Username);
    }

    [Fact]
    public async Task GetAllTraderIdsAsync_ReturnsAllIds()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 100, "Alice");
        await HelpMethods.RegisterTrader(db, 200, "Bob");

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetAllTraderIdsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var ids));
        Assert.Equal(2, ids.Count);
        Assert.Contains(100, ids);
        Assert.Contains(200, ids);
    }

    [Fact]
    public async Task GetAllTraderIdsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetAllTraderIdsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var ids));
        Assert.Empty(ids);
    }

    [Fact]
    public async Task GetTraderCountAsync_ReturnsCorrectCount()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 50, "Alice");
        await HelpMethods.RegisterTrader(db, 1500, "Bob");
        await HelpMethods.RegisterTrader(db, 2000, "Charlie");
        await HelpMethods.RegisterTrader(db, 101, "BotOne");

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderCountAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var count));
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetTraderCountAsync_OnlyBots_ReturnsZero()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "BotOne");
        await HelpMethods.RegisterTrader(db, 200, "BotTwo");

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderCountAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var count));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetTraderCountAsync_EmptyDatabase_ReturnsZero()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderCountAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var count));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetAllTradersWithoutBotsAsync_ReturnsOnlyRealTraders()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "BotOne");
        await HelpMethods.RegisterTrader(db, 50, "Alice");
        await HelpMethods.RegisterTrader(db, 1500, "Bob");
        await HelpMethods.RegisterTrader(db, 2000, "Charlie");

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetAllTradersWithoutBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var traders));
        Assert.Equal(3, traders.Count);
        Assert.Contains(traders, t => t.Username == "Alice" && t.TelegramId == 50);
        Assert.Contains(traders, t => t.Username == "Bob" && t.TelegramId == 1500);
        Assert.Contains(traders, t => t.Username == "Charlie" && t.TelegramId == 2000);
        Assert.DoesNotContain(traders, t => t.Username == "BotOne");
    }

    [Fact]
    public async Task GetAllTradersWithoutBotsAsync_OnlyBots_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "BotOne");
        await HelpMethods.RegisterTrader(db, 200, "BotTwo");

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetAllTradersWithoutBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var traders));
        Assert.Empty(traders);
    }

    [Fact]
    public async Task GetAllTradersWithoutBotsAsync_EmptyDatabase_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetAllTradersWithoutBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var traders));
        Assert.Empty(traders);
    }

    [Fact]
    public async Task GetAllTradersWithoutBotsAsync_TraderWithNoName_ReturnsUnknown()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var trader = ArkWallet.Domain.Entities.Trader.Create(1500, null);
        db.Traders.Add(trader);
        await db.SaveChangesAsync();

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetAllTradersWithoutBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var traders));
        Assert.Single(traders);
        Assert.Equal("Unknown", traders[0].Username);
    }
}
