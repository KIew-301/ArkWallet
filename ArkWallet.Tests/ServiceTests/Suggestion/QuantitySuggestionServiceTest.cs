using ArkWallet.Application.Services.SuggestionServices;
using ArkWallet.Tests.HelpTools;
using Xunit;

namespace ArkWallet.Tests.ServiceTests.Suggestion;

public class QuantitySuggestionServiceTest : IAsyncLifetime
{
    private readonly ArkWallet.Infrastructure.Data.ArkWalletDbContext _db;
    private readonly QuantitySuggestionService _service;

    public QuantitySuggestionServiceTest()
    {
        _db = DbTest.CreateDbContext();
        _service = new QuantitySuggestionService(_db);
    }

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // === Buy ===

    [Fact]
    public async Task GetBuySuggestions_NonExistentTrader_ReturnsEmpty()
    {
        await HelpMethods.CreateToken(_db, "ZZZ", price: 100m);

        var result = await _service.GetBuyQuantitySuggestionsAsync(999, "ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuySuggestions_NonExistentToken_ReturnsEmpty()
    {
        await HelpMethods.RegisterTrader(_db, 101, "User");

        var result = await _service.GetBuyQuantitySuggestionsAsync(101, "NOPE");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuySuggestions_TokenPriceZero_ReturnsEmpty()
    {
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 0m);

        var result = await _service.GetBuyQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuySuggestions_BalanceExactlyTokenPrice_ReturnsSingle()
    {
        // Trader.Create gives 1000 default. Set balance to 100 via GiveMoney won't work (adds).
        // So use a token price that makes 1000 buy exactly 10 at 100%.
        // Balance 1000, price 1000 → 100%=1, 50%=0, etc. → single suggestion
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 1000m);

        var result = await _service.GetBuyQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public async Task GetBuySuggestions_LargeBalance_ReturnsAllPercentages()
    {
        // Balance 1000, price 1 → 100%=1000, 50%=500, 25%=250, 10%=100, 5%=50
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 1m);

        var result = await _service.GetBuyQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Equal(5, result.Count);
        Assert.Equal(1000, result[0].Quantity);
        Assert.Equal(500, result[1].Quantity);
        Assert.Equal(250, result[2].Quantity);
        Assert.Equal(100, result[3].Quantity);
        Assert.Equal(50, result[4].Quantity);
    }

    [Fact]
    public async Task GetBuySuggestions_PriceHigherThanBalance_ReturnsEmpty()
    {
        // Balance 1000, price 50000 → 100%=0, all 0
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 50000m);

        var result = await _service.GetBuyQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBuySuggestions_ReturnsSortedDescending()
    {
        // Balance 1000, price 5 → 100%=200, 50%=100, 25%=50, 10%=20, 5%=10
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 5m);

        var result = await _service.GetBuyQuantitySuggestionsAsync(101, "ZZZ");

        for (int i = 0; i < result.Count - 1; i++)
            Assert.True(result[i].Quantity > result[i + 1].Quantity);
    }

    // === Sell ===

    [Fact]
    public async Task GetSellSuggestions_NoPortfolio_ReturnsEmpty()
    {
        var result = await _service.GetSellQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSellSuggestions_EmptyPortfolio_ReturnsEmpty()
    {
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 100m);
        await HelpMethods.AddPortfolio(_db, 101, "ZZZ", 0);

        var result = await _service.GetSellQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSellSuggestions_SingleToken_ReturnsSingle()
    {
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 100m);
        await HelpMethods.AddPortfolio(_db, 101, "ZZZ", 1);

        var result = await _service.GetSellQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public async Task GetSellSuggestions_TenTokens_ReturnsAllPercentages()
    {
        // 10 tokens → 100%=10, 50%=5, 25%=2, 10%=1, 5%=0(floor) → 4
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 100m);
        await HelpMethods.AddPortfolio(_db, 101, "ZZZ", 10);

        var result = await _service.GetSellQuantitySuggestionsAsync(101, "ZZZ");

        Assert.Equal(4, result.Count);
        Assert.Equal(10, result[0].Quantity);
        Assert.Equal(5, result[1].Quantity);
        Assert.Equal(2, result[2].Quantity);
        Assert.Equal(1, result[3].Quantity);
    }

    [Fact]
    public async Task GetSellSuggestions_ReturnsSortedDescending()
    {
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 100m);
        await HelpMethods.AddPortfolio(_db, 101, "ZZZ", 100);

        var result = await _service.GetSellQuantitySuggestionsAsync(101, "ZZZ");

        for (int i = 0; i < result.Count - 1; i++)
            Assert.True(result[i].Quantity > result[i + 1].Quantity);
    }

    [Fact]
    public async Task GetSellSuggestions_WrongSymbol_ReturnsEmpty()
    {
        await HelpMethods.RegisterTrader(_db, 101, "User");
        await HelpMethods.CreateToken(_db, "ZZZ", price: 100m);
        await HelpMethods.AddPortfolio(_db, 101, "ZZZ", 10);

        var result = await _service.GetSellQuantitySuggestionsAsync(101, "NOPE");

        Assert.Empty(result);
    }
}
