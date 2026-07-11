using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;

namespace ArkWallet.Tests.DomainTests;

public class PortfolioItemTest
{
    private static PortfolioItem CreateItem(
        int quantity = 100,
        decimal price = 10m) =>
        PortfolioItem.Create(101L, "AAA", quantity, price);

    private static CharacterToken CreateToken(decimal currentPrice = 15m) =>
        CharacterToken.Create("AAA", "Token A", Domain.ValueObjects.CharacterRarity.OneStar,
            currentPrice, 1000, "https://img.png", "https://icon.png");

    [Fact]
    public void Create_ValidData_ReturnsItem()
    {
        var item = CreateItem();

        Assert.Equal(101L, item.TraderTelegramId);
        Assert.Equal("AAA", item.CharacterTokenId);
        Assert.Equal(100, item.Quantity);
        Assert.Equal(10m, item.AverageBuyPrice);
    }

    [Fact]
    public void Create_ZeroQuantity_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateItem(quantity: 0));
    }

    [Fact]
    public void Create_SetsId()
    {
        var item = CreateItem();

        Assert.False(string.IsNullOrEmpty(item.Id));
    }

    [Fact]
    public void Create_SetsAcquiredAt()
    {
        var before = DateTime.UtcNow;
        var item = CreateItem();

        Assert.True(item.AcquiredAt >= before);
    }

    [Fact]
    public void MarkDirty_ThenClean_TogglesIsDirty()
    {
        var item = CreateItem();

        item.MarkDirty();
        Assert.True(item.IsDirty);

        item.MarkClean();
        Assert.False(item.IsDirty);
    }

    [Fact]
    public void GetTotalValue_ReturnsQuantityTimesPrice()
    {
        var item = CreateItem(quantity: 50, price: 20m);

        Assert.Equal(1000m, item.GetTotalValue());
    }

    [Fact]
    public void GetCurrentValue_ReturnsQuantityTimesCurrentPrice()
    {
        var item = CreateItem(quantity: 10, price: 5m);
        var token = CreateToken(currentPrice: 30m);

        Assert.Equal(300m, item.GetCurrentValue(token));
    }

    [Fact]
    public void GetProfitLoss_ReturnsDifference()
    {
        var item = CreateItem(quantity: 10, price: 5m);
        var token = CreateToken(currentPrice: 8m);

        // TotalValue=50, CurrentValue=80, ProfitLoss=30
        Assert.Equal(30m, item.GetProfitLoss(token));
    }

    [Fact]
    public void BuyTokens_IncreasesQuantityAndAveragesPrice()
    {
        var item = CreateItem(quantity: 10, price: 10m);

        item.BuyTokens(10, 20m);

        Assert.Equal(20, item.Quantity);
        Assert.Equal(15m, item.AverageBuyPrice); // (10*10 + 10*20) / 20
        Assert.True(item.IsDirty);
    }

    [Fact]
    public void BuyTokens_ZeroQuantity_ThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.BuyTokens(0, 10m));
    }

    [Fact]
    public void ReserveTokens_TransfersQuantityToReserve()
    {
        var item = CreateItem(quantity: 100, price: 10m);

        item.ReserveTokens(30, 12m);

        Assert.Equal(70, item.Quantity);
        Assert.Equal(30, item.ReserveQuantity);
        Assert.Equal(12m, item.AverageReservePrice);
        Assert.True(item.IsDirty);
    }

    [Fact]
    public void ReserveTokens_ZeroQuantity_ThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.ReserveTokens(0, 10m));
    }

    [Fact]
    public void ReserveTokens_AllTokensZeroesBuyPrice()
    {
        var item = CreateItem(quantity: 10, price: 10m);

        item.ReserveTokens(10, 15m);

        Assert.Equal(0, item.Quantity);
        Assert.Equal(0m, item.AverageBuyPrice);
    }

    [Fact]
    public void SellTokens_TransfersFromReserveToSelling()
    {
        var item = CreateItem(quantity: 100, price: 10m);
        item.ReserveTokens(50, 12m);

        item.SellTokens(20, 15m);

        Assert.Equal(30, item.ReserveQuantity);
        Assert.Equal(20, item.SellingQuantity);
        Assert.Equal(15m, item.AverageSellPrice);
    }

    [Fact]
    public void SellTokens_ZeroQuantity_ThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.SellTokens(0, 10m));
    }

    [Fact]
    public void ReturnTokens_TransfersFromReserveBackToQuantity()
    {
        var item = CreateItem(quantity: 100, price: 10m);
        item.ReserveTokens(30, 12m);

        item.ReturnTokens(10);

        Assert.Equal(80, item.Quantity);
        Assert.Equal(20, item.ReserveQuantity);
    }

    [Fact]
    public void ReturnTokens_ZeroQuantity_ThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.ReturnTokens(0));
    }

    [Fact]
    public void RemoveTokens_DecreasesQuantity()
    {
        var item = CreateItem(quantity: 100, price: 10m);

        item.RemoveTokens(30, 10m);

        Assert.Equal(70, item.Quantity);
        Assert.True(item.IsDirty);
    }

    [Fact]
    public void RemoveTokens_ZeroQuantity_ThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.RemoveTokens(0, 10m));
    }

    [Fact]
    public void RemoveTokens_MoreThanAvailable_ThrowsDomainException()
    {
        var item = CreateItem(quantity: 10);

        Assert.Throws<DomainException>(() => item.RemoveTokens(20, 10m));
    }

    [Fact]
    public void RemoveTokens_RemovesAllTokensZeroesBuyPrice()
    {
        var item = CreateItem(quantity: 10, price: 10m);

        item.RemoveTokens(10, 10m);

        Assert.Equal(0, item.Quantity);
        Assert.Equal(0m, item.AverageBuyPrice);
    }
}
