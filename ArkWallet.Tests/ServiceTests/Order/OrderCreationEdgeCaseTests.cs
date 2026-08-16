using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderCreationEdgeCaseTests
{
    [Fact]
    public async Task ProcessOrderAsync_TokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.GiveMoney(db, 101, 10000);

        var result = await HelpMethods.PlaceOrder(db, 101, "\u043A\u0443\u043F\u0438\u0442\u044C", "UNKNOWN", 5, 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("\u0422\u043E\u043A\u0435\u043D\u0430 \u043D\u0435 \u0441\u0443\u0449\u0435\u0441\u0442\u0432\u0443\u0435\u0442", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_TraderNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var result = await HelpMethods.PlaceOrder(db, 999, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("\u041F\u043E\u043B\u044C\u0437\u043E\u0432\u0430\u0442\u0435\u043B\u044F \u043D\u0435 \u0441\u0443\u0449\u0435\u0441\u0442\u0432\u0443\u0435\u0442", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_InsufficientBalance_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var result = await HelpMethods.PlaceOrder(db, 101, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 10000);

        Assert.False(result.IsSuccess);
        Assert.Contains("Insufficient balance", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_SellInsufficientTokens_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 3);

        var result = await HelpMethods.PlaceOrder(db, 101, "\u043F\u0440\u043E\u0434\u0430\u0442\u044C", "ZZZ", 5, 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("Not enough tokens in portfolio", result.Message);
    }
}
