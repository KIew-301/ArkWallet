using ArkWallet.Application.Services.TraderServices;

namespace ArkWallet.Tests;

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

        var traderRegistrationService = new TraderRegistrationService(db);
        var result = await traderRegistrationService.RegisterTraderAsync(id, name);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterUserAsync_WhenUserAlreadyExists_ReturnsFailure()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        Random rnd = new();

        long id = rnd.NextInt64(1, 1_000_000_000_000_000);
        string name = "Kuro";

        var traderRegistrationService = new TraderRegistrationService(db);

        var result1 = await traderRegistrationService.RegisterTraderAsync(id, name);
        var result2 = await traderRegistrationService.RegisterTraderAsync(id, name);

        Assert.True(result1.IsSuccess);
        Assert.False(result2.IsSuccess);
    }
}
