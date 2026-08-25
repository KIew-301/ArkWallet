using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class AppStateQueryServiceTest
{
    private static readonly string[] ExpectedKeysOrdered = ["A", "B", "C"];

    [Fact]
    public async Task TakeAllAsync_NoStates_ReturnsEmpty()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new AppStateQueryService(db, NullLogger<AppStateQueryService>.Instance);

        var result = await service.TakeAllAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var states));
        Assert.Empty(states);
    }

    [Fact]
    public async Task TakeAllAsync_WithStates_ReturnsAllOrderedByKey()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.AppStates.Add(AppState.Create("B", new { value = 2 }));
        db.AppStates.Add(AppState.Create("A", new { value = 1 }));
        db.AppStates.Add(AppState.Create("C", new { value = 3 }));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AppStateQueryService(db, NullLogger<AppStateQueryService>.Instance);

        var result = await service.TakeAllAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var states));
        Assert.Equal(3, states.Count);
        Assert.Equal(ExpectedKeysOrdered, states.Select(s => s.Key));
        Assert.All(states, s => Assert.False(string.IsNullOrEmpty(s.Value)));
    }
}
