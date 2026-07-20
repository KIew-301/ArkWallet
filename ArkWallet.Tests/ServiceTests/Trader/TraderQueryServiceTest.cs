using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Trader;

public class TraderQueryServiceTest
{
    [Fact]
    public async Task GetTraderProfileAsync_TraderNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderProfileAsync(999);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetTraderProfileAsync_TraderExists_ReturnsProfile()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "TestUser");
        await HelpMethods.GiveMoney(db, 101, 500m);

        var service = new TraderQueryService(db, NullLogger<TraderQueryService>.Instance);

        var result = await service.GetTraderProfileAsync(101);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("TestUser", data.Username);
        Assert.Equal(1500m, data.Balance);
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
}
