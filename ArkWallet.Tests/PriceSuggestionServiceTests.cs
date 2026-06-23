using ArkWallet.Application.Services.SuggestionServices;

namespace ArkWallet.Tests;

public class PriceSuggestionServiceTests
{
    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_TraderAndOrdersExist_ReturnsFilteredSuggestions()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 90);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        Assert.NotEmpty(result);
        Assert.All(result, dto => Assert.True(dto.Price <= 200));
        Assert.All(result, dto => Assert.NotNull(dto.Description));
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_TraderNotFound_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(999, "ZZZ", 5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_NoLongOrders_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_NoShortOrders_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_NoShortOrders_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_NoLongOrder_ReturnsEmpty()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_PricesAreSortedAscending()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 90);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        var prices = result.Select(dto => dto.Price).ToList();
        var sorted = prices.OrderBy(p => p).ToList();
        Assert.Equal(sorted, prices);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_PricesAreSortedDescending()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 130);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        var prices = result.Select(dto => dto.Price).ToList();
        var sorted = prices.OrderByDescending(p => p).ToList();
        Assert.Equal(sorted, prices);
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_PriceCalculationsAreCorrect()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 400);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 600);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 1000);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        Assert.Equal(400, result[0].Price);
        Assert.Equal(500, result[1].Price);
        Assert.Equal(600, result[2].Price);
        Assert.Equal(1000, result[3].Price);
        Assert.Equal(1200, result[4].Price);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_PriceCalculationsAreCorrect()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 130);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        Assert.Equal(130, result[0].Price);
        Assert.Equal(125, result[1].Price);
        Assert.Equal(120, result[2].Price);
        Assert.Equal(100, result[3].Price);
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_ReturnsOnlyUniquePrices()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        var uniquePrices = result.Select(dto => dto.Price).Distinct().Count();
        Assert.Equal(result.Count, uniquePrices);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_ReturnsOnlyUniquePrices()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        var uniquePrices = result.Select(dto => dto.Price).Distinct().Count();
        Assert.Equal(result.Count, uniquePrices);
    }
}