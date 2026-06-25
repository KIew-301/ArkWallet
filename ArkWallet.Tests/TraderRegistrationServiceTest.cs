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
}