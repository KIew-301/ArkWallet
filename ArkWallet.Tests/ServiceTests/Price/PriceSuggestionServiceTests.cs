using ArkWallet.Application.Services.SuggestionServices;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Price;

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

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_LowBalance_FiltersExpensiveSuggestions()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 300);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        var maxPrice = Math.Floor(1000m / 5);
        Assert.All(result, dto => Assert.True(dto.Price <= maxPrice));
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_SingleLongOrder_GreatPriceEqualsCurrentPrice()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 150);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        Assert.NotEmpty(result);
        Assert.All(result, dto => Assert.True(dto.Price > 0));
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_SingleShortOrder_GreatPriceEqualsMarketPrice()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 80);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 120);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        Assert.NotEmpty(result);
        Assert.All(result, dto => Assert.True(dto.Price > 0));
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_MultipleBuyOrders_AveragePriceCalculated()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 100000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 300);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 400);

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        var goodPrice = result.FirstOrDefault(dto => dto.Label == "Оптимальная цена");
        Assert.NotNull(goodPrice);
        Assert.Equal(200m, goodPrice.Price);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_MultipleShortOrders_AveragePriceCalculated()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 300);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        var goodPrice = result.FirstOrDefault(dto => dto.Label == "Оптимальная цена");
        Assert.NotNull(goodPrice);
        Assert.Equal(200m, goodPrice.Price);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_UsesBestBid_NotCheapestBuy()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 100000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 150);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 300);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        var truePrice = result.FirstOrDefault(dto => dto.Label == "Истинная цена");
        Assert.NotNull(truePrice);
        Assert.Equal(200m, truePrice.Price);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_CancelledOrdersExcluded()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 100000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 300);

        var orders = db.TradeOrders.ToList();
        var expensiveBuy = orders.First(o => o.Type == Domain.ValueObjects.OrderType.Buy && o.Price == 50);
        expensiveBuy.Status = Domain.ValueObjects.OrderStatus.Cancelled;
        var cheapSell = orders.First(o => o.Type == Domain.ValueObjects.OrderType.Sell && o.Price == 200);
        cheapSell.Status = Domain.ValueObjects.OrderStatus.Filled;
        await db.SaveChangesAsync();

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        var truePrice = result.FirstOrDefault(dto => dto.Label == "Истинная цена");
        Assert.NotNull(truePrice);
        Assert.Equal(100m, truePrice.Price);

        var marketPrice = result.FirstOrDefault(dto => dto.Label == "Рыночная цена");
        Assert.NotNull(marketPrice);
        Assert.Equal(300m, marketPrice.Price);
    }

    [Fact]
    public async Task GetSellPriceSuggestionsAsync_GreatPriceIsMostExpensiveSell()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 100000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 300);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 400);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 500);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 600);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 700);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 800);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 900);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 1000);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 1100);

        var service = new PriceSuggestionService(db);

        var result = await service.GetSellPriceSuggestionsAsync("ZZZ");

        var greatPrice = result.FirstOrDefault(dto => dto.Label == "Завышенная цена");
        Assert.NotNull(greatPrice);
        Assert.Equal(1100m, greatPrice.Price);
    }

    [Fact]
    public async Task GetBuyPriceSuggestionsAsync_CancelledOrdersExcluded()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 100000);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 300);

        var orders = db.TradeOrders.ToList();
        var cheapBuy = orders.First(o => o.Type == Domain.ValueObjects.OrderType.Buy && o.Price == 100);
        cheapBuy.Status = Domain.ValueObjects.OrderStatus.Cancelled;
        await db.SaveChangesAsync();

        var service = new PriceSuggestionService(db);

        var result = await service.GetBuyPriceSuggestionsAsync(101, "ZZZ", 5);

        var truePrice = result.FirstOrDefault(dto => dto.Label == "Истинная цена");
        Assert.NotNull(truePrice);
        Assert.Equal(200m, truePrice.Price);
    }
}
