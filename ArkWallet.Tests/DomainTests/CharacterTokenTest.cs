using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests.DomainTests;

public class CharacterTokenTest
{
    private static CharacterToken CreateValidToken(
        string symbol = "AAA",
        string name = "Token A",
        decimal price = 10m,
        int supply = 1000) =>
        CharacterToken.Create(symbol, name, CharacterRarity.ThreeStar, price, supply, "https://img.png", "https://icon.png");

    [Fact]
    public void Create_ValidData_ReturnsToken()
    {
        var token = CreateValidToken();

        Assert.Equal("AAA", token.Symbol);
        Assert.Equal("Token A", token.Name);
        Assert.Equal(CharacterRarity.ThreeStar, token.Rarity);
        Assert.Equal(10m, token.CurrentPrice);
        Assert.Equal(1000, token.TotalSupply);
        Assert.True(token.IsActive);
        Assert.Equal("https://img.png", token.ImageUrl);
        Assert.Equal("https://icon.png", token.IconUrl);
    }

    [Fact]
    public void Create_NullSymbol_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateValidToken(symbol: null!));
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateValidToken(name: ""));
    }

    [Fact]
    public void Create_ZeroPrice_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateValidToken(price: 0));
    }

    [Fact]
    public void Create_ZeroSupply_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateValidToken(supply: 0));
    }

    [Fact]
    public void Create_NullImageUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterToken.Create("AAA", "A", CharacterRarity.OneStar, 10m, 100, null!, "https://icon.png"));
    }

    [Fact]
    public void Create_NullIconUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterToken.Create("AAA", "A", CharacterRarity.OneStar, 10m, 100, "https://img.png", null!));
    }

    [Fact]
    public void CanBeTraded_ActiveWithSupply_ReturnsTrue()
    {
        var token = CreateValidToken();

        Assert.True(token.CanBeTraded());
    }

    [Fact]
    public void Create_SetsCreatedAtToUtcNow()
    {
        var before = DateTime.UtcNow;
        var token = CreateValidToken();

        Assert.True(token.CreatedAt >= before);
        Assert.True(token.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_DefaultsToActive()
    {
        var token = CreateValidToken();

        Assert.True(token.IsActive);
    }

    [Fact]
    public void UpdatePrice_NegativePrice_ThrowsArgumentException()
    {
        var token = CreateValidToken();

        Assert.Throws<ArgumentException>(() => token.UpdatePrice(-1m));
    }

    [Fact]
    public void UpdatePrice_ZeroPrice_SetsPrice()
    {
        var token = CreateValidToken();

        token.UpdatePrice(0m);

        Assert.Equal(0m, token.CurrentPrice);
    }

    [Fact]
    public void CalculateMarketCap_ReturnsPriceTimesSupply()
    {
        var token = CreateValidToken(price: 25m, supply: 40);

        Assert.Equal(1000m, token.CalculateMarketCap());
    }
}
