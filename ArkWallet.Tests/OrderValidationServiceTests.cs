using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.Other;

namespace ArkWallet.Tests;

public class OrderValidationServiceTests
{
    [Theory]
    [InlineData("купить", true)]
    [InlineData("продать", true)]
    [InlineData("КУПИТЬ", false)]
    [InlineData("покупать", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateDirection_ShouldReturnExpectedResult(string direction, bool expected)
    {
        using var db = DbTest.CreateDbContext();
        var service = new OrderValidationService(db, new ReserveCalculationService(db));

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
        var service = new OrderValidationService(db, new ReserveCalculationService(db));

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
        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = service.ValidateQuantity(quantity);

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_BuyDirection_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateTokenAsync(101, "ZZZ", "купить");

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_SellWithToken_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateTokenAsync(101, "ZZZ", "продать");

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_SellWithoutToken_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateTokenAsync(101, "ZZZ", "продать");

        Assert.False(result.IsValid);
        Assert.Contains("не обладает", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_ActiveOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        var orderResult = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCancellationAsync(101, orderResult.Order.Id);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_OrderNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCancellationAsync(101, "non-existent-id");

        Assert.False(result.IsValid);
        Assert.Contains("не существует", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_InactiveOrder_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        var orderResult = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        await HelpMethods.CancelOrder(db, 101, orderResult.Order.Id);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCancellationAsync(101, orderResult.Order.Id);

        Assert.False(result.IsValid);
        Assert.Contains("Нельзя отменить неактивный ордер", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCancellationAsync_NotTraderOrder_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        var orderResult = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCancellationAsync(102, orderResult.Order.Id);

        Assert.False(result.IsValid);
        Assert.Contains("не своей", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCreation_Buy_SufficientBalance_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCreationAsync(101, "ZZZ", "купить", 5, 100);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateOrderCreation_Buy_InsufficientBalance_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCreationAsync(101, "ZZZ", "купить", 15, 100);

        Assert.False(result.IsValid);
        Assert.Contains("Не хватает средств", result.Message);
    }

    [Fact]
    public async Task ValidateOrderCreation_Sell_SufficientTokens_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCreationAsync(101, "ZZZ", "продать", 5, 100);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateOrderCreation_Sell_InsufficientTokens_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();
        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 3);

        var service = new OrderValidationService(db, new ReserveCalculationService(db));

        var result = await service.ValidateOrderCreationAsync(101, "ZZZ", "продать", 5, 100);

        Assert.False(result.IsValid);
        Assert.Contains("Не хватает токенов", result.Message);
    }
}