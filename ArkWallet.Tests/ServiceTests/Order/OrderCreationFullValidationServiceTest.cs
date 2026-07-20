using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderCreationFullValidationServiceTest
{
    [Fact]
    public async Task ValidateAsync_AllValidationsPass_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);

        var validationService = new OrderValidationService(db);

        var request = new CreateOrderCommand(
            TraderId: 101,
            Direction: "купить",
            Symbol: "ZZZ",
            Quantity: 5,
            Price: 100
        );

        var result = await validationService.ValidateFullOrderAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_InvalidPrice_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var validationService = new OrderValidationService(db);

        var request = new CreateOrderCommand(
            TraderId: 101,
            Direction: "купить",
            Symbol: "ZZZ",
            Quantity: 5,
            Price: 0
        );

        var result = await validationService.ValidateFullOrderAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains("Цена должна быть больше 0", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_InvalidQuantity_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var validationService = new OrderValidationService(db);

        var request = new CreateOrderCommand(
            TraderId: 101,
            Direction: "купить",
            Symbol: "ZZZ",
            Quantity: 0,
            Price: 100
        );

        var result = await validationService.ValidateFullOrderAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains("Количество должно быть больше 0", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_TokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);

        var validationService = new OrderValidationService(db);

        var request = new CreateOrderCommand(
            TraderId: 101,
            Direction: "продать",
            Symbol: "UNKNOWN",
            Quantity: 5,
            Price: 100
        );

        var result = await validationService.ValidateFullOrderAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains("не обладает", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_NotTokenExistence_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var validationService = new OrderValidationService(db);

        var request = new CreateOrderCommand(
            TraderId: 101,
            Direction: "продать",
            Symbol: "ZZZ",
            Quantity: 5,
            Price: 100
        );

        var result = await validationService.ValidateFullOrderAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains("не обладает", result.Message);
    }
}
