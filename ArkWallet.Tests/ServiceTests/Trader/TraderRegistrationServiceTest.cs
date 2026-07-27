using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Trader;

public class TraderRegistrationServiceTest
{
    [Theory]
    [InlineData(0, "Test", "Некорректный ID пользователя 0")]
    [InlineData(-50, "Test", "Некорректный ID пользователя -50")]
    [InlineData(1, "", "Имя не может быть пустым")]
    public async Task RegisterUserAsync_WithInvalidData_ReturnsFailure(long id, string name, string errorMessage)
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var result = await HelpMethods.RegisterTrader(db, id, name);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorMessage, result.Message);
    }

    [Fact]
    public async Task RegisterUserAsync_WhenUserAlreadyExists_ReturnsFailure()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        long id = Random.Shared.NextInt64(1, 1_000_000_000_000_000);
        string name = "Kuro";

        var result1 = await HelpMethods.RegisterTrader(db, id, name);
        var result2 = await HelpMethods.RegisterTrader(db, id, name);

        Assert.True(result1.IsSuccess);
        Assert.False(result2.IsSuccess);
    }

    [Fact]
    public async Task GetAllTraderIdsAsync_ReturnsAllIds()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TraderRegistrationService(db, NullLogger<TraderRegistrationService>.Instance);

        await service.RegisterTraderAsync(100, "Alice");
        await service.RegisterTraderAsync(200, "Bob");

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

        var service = new TraderRegistrationService(db, NullLogger<TraderRegistrationService>.Instance);

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

        var service = new TraderRegistrationService(db, NullLogger<TraderRegistrationService>.Instance);

        await service.RegisterTraderAsync(100, "Alice");
        await service.RegisterTraderAsync(200, "Bob");
        await service.RegisterTraderAsync(300, "Charlie");

        var result = await service.GetTraderCountAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var count));
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetTraderCountAsync_EmptyDatabase_ReturnsZero()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new TraderRegistrationService(db, NullLogger<TraderRegistrationService>.Instance);

        var result = await service.GetTraderCountAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var count));
        Assert.Equal(0, count);
    }
}