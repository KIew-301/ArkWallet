using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests;
public class OrderCreationServiceTest
{
    private record TestTrader(long TelegramId, string Name);
    private record TestToken(string Symbol, string Name, CharacterRarity Rarity, int TotalSupply, int CurrentPrice, bool IsActive);
    private record TestOrder(long TraderId, string Direction, string Symbol, int Quantity, decimal Price);
    private record TestPortfolio(long TraderId, string Symbol, int Quantity);

    [Fact]
    public async Task ProcessOrdersAsync_MatchingTest_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "FirstUser");
        await HelpMethods.RegisterTrader(db, 102, "SecondUser");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        var result1 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        var result2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleLongOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        var result = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, $"Order failed: {result.ErrorMessage}");
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleShortOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);
        var result = await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, $"Order failed: {result.ErrorMessage}");
    }
}
