using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderValidationFullOrderTests
{
    [Fact]
    public async Task ValidateFullOrderAsync_InvalidPrice_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrderAsync_InvalidQuantity_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 0, 100));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrderAsync_AllValid_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 100));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrdersAsync_Empty_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrdersAsync(Array.Empty<CreateOrderCommand>());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrdersAsync_InvalidPrice_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var requests = new[]
        {
            new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 0),
            new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 3, 100),
        };

        var result = await service.ValidateFullOrdersAsync(requests);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrdersAsync_InvalidQuantity_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var requests = new[]
        {
            new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 0, 100),
            new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 100),
        };

        var result = await service.ValidateFullOrdersAsync(requests);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrdersAsync_MixedGroupAllValid_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);

        var service = new OrderValidationService(db);

        var requests = new[]
        {
            new CreateOrderCommand(1001, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 100),
            new CreateOrderCommand(1001, "\u043F\u0440\u043E\u0434\u0430\u0442\u044C", "ZZZ", 5, 100),
        };

        var result = await service.ValidateFullOrdersAsync(requests);

        Assert.True(result.IsValid);
    }
}
