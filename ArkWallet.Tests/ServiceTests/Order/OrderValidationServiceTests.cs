using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderValidationServiceTests
{
    [Theory]
    [InlineData("купить", true)]
    [InlineData("продать", true)]
    [InlineData("КУПИТЬ", true)]
    [InlineData(" Купить ", true)]
    [InlineData("покупать", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateDirection_ShouldReturnExpectedResult(string direction, bool expected)
    {
        using var db = DbTest.CreateDbContext();
        var service = new OrderValidationService(db);

        var result = service.ValidateDirection(direction);

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-10, false)]
    public void ValidatePrice_ShouldReturnExpectedResult(decimal price, bool expected)
    {
        using var db = DbTest.CreateDbContext();
        var service = new OrderValidationService(db);

        var result = service.ValidatePrice(price);

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public void ValidateQuantity_ShouldReturnExpectedResult(int quantity, bool expected)
    {
        using var db = DbTest.CreateDbContext();
        var service = new OrderValidationService(db);

        var result = service.ValidateQuantity(quantity);

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_ActiveOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        var orderResult = await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var service = new OrderValidationService(db);

        Assert.True(orderResult.TryGetData(out var data), orderResult.Message);
        var result = await service.ValidateOrderCancellationAsync(1001, data.Order.Id);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_OrderNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);

        var service = new OrderValidationService(db);

        var result = await service.ValidateOrderCancellationAsync(1001, "non-existent-id");

        Assert.False(result.IsValid);
        Assert.Contains("не существует", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_InactiveOrder_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        var orderResult = await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        await HelpMethods.CancelOrder(db, 1001, orderResult);

        var service = new OrderValidationService(db);

        Assert.True(orderResult.TryGetData(out var data), orderResult.Message);
        var result = await service.ValidateOrderCancellationAsync(1001, data.Order.Id);

        Assert.False(result.IsValid);
        Assert.Contains("Нельзя отменить неактивный ордер", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_NotTraderOrder_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ");
        var orderResult = await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var service = new OrderValidationService(db);

        Assert.True(orderResult.TryGetData(out var data), orderResult.Message);
        var result = await service.ValidateOrderCancellationAsync(1002, data.Order.Id);

        Assert.False(result.IsValid);
        Assert.Contains("не своей", result.Message);
    }
}