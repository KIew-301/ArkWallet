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
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(101, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrderAsync_InvalidQuantity_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(101, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 0, 100));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrderAsync_SellWithoutTokens_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(101, "\u043F\u0440\u043E\u0434\u0430\u0442\u044C", "ZZZ", 5, 100));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateFullOrderAsync_AllValid_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db);

        var result = await service.ValidateFullOrderAsync(new CreateOrderCommand(101, "\u043A\u0443\u043F\u0438\u0442\u044C", "ZZZ", 5, 100));

        Assert.True(result.IsValid);
    }
}
