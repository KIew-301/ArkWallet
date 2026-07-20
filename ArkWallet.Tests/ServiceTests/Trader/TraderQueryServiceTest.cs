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
}
