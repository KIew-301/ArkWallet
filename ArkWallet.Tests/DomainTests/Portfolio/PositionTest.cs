using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.PortfolioContext;

namespace ArkWallet.Tests.DomainTests.Portfolio;

public class PositionTest
{
    private static Position CreatePosition(int quantity = 100, decimal price = 10m) =>
        Position.Create(1001, "ZZZ", quantity, price);

    [Fact]
    public void Create_SetsFields()
    {
        var p = CreatePosition();

        Assert.Equal(1001, p.TraderTelegramId);
        Assert.Equal("ZZZ", p.Symbol);
        Assert.Equal(100, p.Quantity);
        Assert.Equal(10m, p.AverageBuyPrice);
        Assert.Equal(0, p.SellingQuantity);
        Assert.Equal(0, p.ReserveQuantity);
        Assert.False(string.IsNullOrWhiteSpace(p.Id));
        Assert.False(p.IsEmpty);
    }

    [Fact]
    public void Create_ZeroQuantity_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => CreatePosition(quantity: 0));
        Assert.Equal("Для обновление портфеля необходим минимум один токен", ex.Message);
    }

    [Fact]
    public void GetTotalValue_ReturnsQuantityTimesAvgBuy()
    {
        Assert.Equal(1000m, CreatePosition(100, 10m).GetTotalValue());
    }

    [Fact]
    public void GetCurrentValue_ReturnsQuantityTimesPrice()
    {
        Assert.Equal(500m, CreatePosition(100, 10m).GetCurrentValue(5m));
    }

    [Fact]
    public void GetProfitLoss_ReturnsDifference()
    {
        Assert.Equal(-500m, CreatePosition(100, 10m).GetProfitLoss(5m));
    }

    [Fact]
    public void BuyTokens_IncreasesQuantityAndRecalculatesAvgBuy()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.BuyTokens(10, 20m);

        Assert.Equal(20, p.Quantity);
        Assert.Equal(15m, p.AverageBuyPrice);
    }

    [Fact]
    public void BuyTokens_NonPositiveQuantity_Throws()
    {
        var p = CreatePosition();
        Assert.Throws<DomainException>(() => p.BuyTokens(0, 10m));
        Assert.Throws<DomainException>(() => p.BuyTokens(-1, 10m));
    }

    [Fact]
    public void ReserveTokens_MovesToReserve_AndRecalculatesAverage()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.ReserveTokens(4, 20m);

        Assert.Equal(6, p.Quantity);
        Assert.Equal(4, p.ReserveQuantity);
        Assert.Equal(20m, p.AverageReservePrice);
    }

    [Fact]
    public void ReserveTokens_ToZeroQuantity_ResetsAverageBuy()
    {
        var p = CreatePosition(quantity: 4, price: 10m);

        p.ReserveTokens(4, 20m);

        Assert.Equal(0, p.Quantity);
        Assert.Equal(0m, p.AverageBuyPrice);
    }

    [Fact]
    public void SellTokens_MovesReserveToSold_AndRecalculatesAverage()
    {
        var p = CreatePosition(quantity: 10, price: 10m);
        p.ReserveTokens(4, 20m);

        p.SellTokens(4, 30m);

        Assert.Equal(4, p.SellingQuantity);
        Assert.Equal(0, p.ReserveQuantity);
        Assert.Equal(30m, p.AverageSellPrice);
    }

    [Fact]
    public void ReturnTokens_MovesReserveBackToAvailable()
    {
        var p = CreatePosition(quantity: 10, price: 10m);
        p.ReserveTokens(4, 20m);

        p.ReturnTokens(4);

        Assert.Equal(10, p.Quantity);
        Assert.Equal(0, p.ReserveQuantity);
        Assert.Equal(14m, p.AverageBuyPrice);
    }

    [Fact]
    public void RemoveTokens_DecreasesQuantity()
    {
        var p = CreatePosition(quantity: 5, price: 10m);

        p.RemoveTokens(3, 10m);

        Assert.Equal(2, p.Quantity);
    }

    [Fact]
    public void RemoveTokens_MoreThanAvailable_Throws()
    {
        var p = CreatePosition(quantity: 5, price: 10m);
        Assert.Throws<DomainException>(() => p.RemoveTokens(6, 10m));
    }

    [Fact]
    public void RemoveTokens_ToZero_ResetsAverageBuy()
    {
        var p = CreatePosition(quantity: 5, price: 10m);
        p.RemoveTokens(5, 10m);

        Assert.Equal(0, p.Quantity);
        Assert.Equal(0m, p.AverageBuyPrice);
    }

    [Fact]
    public void ApplyState_ReplacesFullState()
    {
        var p = CreatePosition();

        p.ApplyState(1, 2, 3, 4m, 5m, 6m);

        Assert.Equal(1, p.Quantity);
        Assert.Equal(2, p.SellingQuantity);
        Assert.Equal(3, p.ReserveQuantity);
        Assert.Equal(4m, p.AverageBuyPrice);
        Assert.Equal(5m, p.AverageSellPrice);
        Assert.Equal(6m, p.AverageReservePrice);
    }

    [Fact]
    public void IsEmpty_OnlyWhenAllZero()
    {
        var p = CreatePosition(quantity: 1, price: 10m);
        p.RemoveTokens(1, 10m);
        Assert.True(p.IsEmpty);
        Assert.False(CreatePosition().IsEmpty);
    }

    [Fact]
    public void CreateOrUpdate_GreaterQuantity_BuysShortfall()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.CreateOrUpdate(15, 20m);

        Assert.Equal(15, p.Quantity);
        Assert.Equal((10*10m + 5*20m) / 15m, p.AverageBuyPrice);
    }

    [Fact]
    public void CreateOrUpdate_LesserQuantity_ReleasesSurplusToSold()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.CreateOrUpdate(6, 20m);

        Assert.Equal(6, p.Quantity);
        Assert.Equal(4, p.SellingQuantity);
    }

    [Fact]
    public void AddTokens_IncreasesQuantity_WithoutChangingAvgBuy()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.AddTokens(5);

        Assert.Equal(15, p.Quantity);
        Assert.Equal(10m, p.AverageBuyPrice);
    }

    [Fact]
    public void AddTokens_NonPositive_Throws()
    {
        var p = CreatePosition();
        Assert.Throws<DomainException>(() => p.AddTokens(0));
        Assert.Throws<DomainException>(() => p.AddTokens(-2));
    }

    [Theory]
    [InlineData(PortfolioChangeType.Buy)]
    [InlineData(PortfolioChangeType.Add)]
    public void ChangePosition_AddsQuantity_WhenBuyOrAdd(PortfolioChangeType type)
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.ChangePosition(new PortfolioChangeCommand(1001, "ZZZ", type, 5, type == PortfolioChangeType.Buy ? 20m : 0m));

        Assert.Equal(15, p.Quantity);
        if (type == PortfolioChangeType.Buy)
            Assert.Equal((10*10m + 5*20m) / 15m, p.AverageBuyPrice);
        else
            Assert.Equal(10m, p.AverageBuyPrice);
    }

    [Fact]
    public void ChangePosition_ReserveType_MovesToReserve()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.ChangePosition(new PortfolioChangeCommand(1001, "ZZZ", PortfolioChangeType.Reserve, 4, 20m));

        Assert.Equal(6, p.Quantity);
        Assert.Equal(4, p.ReserveQuantity);
    }

    [Fact]
    public void ChangePosition_SellType_SellsReserved()
    {
        var p = CreatePosition(quantity: 10, price: 10m);
        p.ReserveTokens(4, 20m);

        p.ChangePosition(new PortfolioChangeCommand(1001, "ZZZ", PortfolioChangeType.Sell, 4, 30m));

        Assert.Equal(4, p.SellingQuantity);
        Assert.Equal(0, p.ReserveQuantity);
    }

    [Fact]
    public void ChangePosition_ReturnType_ReturnsToAvailable()
    {
        var p = CreatePosition(quantity: 10, price: 10m);
        p.ReserveTokens(4, 20m);

        p.ChangePosition(new PortfolioChangeCommand(1001, "ZZZ", PortfolioChangeType.Return, 4, 0m));

        Assert.Equal(10, p.Quantity);
        Assert.Equal(0, p.ReserveQuantity);
    }

    [Fact]
    public void ChangePosition_RemoveType_RemovesQuantity()
    {
        var p = CreatePosition(quantity: 10, price: 10m);

        p.ChangePosition(new PortfolioChangeCommand(1001, "ZZZ", PortfolioChangeType.Remove, 4, 10m));

        Assert.Equal(6, p.Quantity);
    }

    [Fact]
    public void ChangePosition_UnknownType_Throws()
    {
        var p = CreatePosition();
        var unknown = (PortfolioChangeType)999;

        var ex = Assert.Throws<DomainException>(() =>
            p.ChangePosition(new PortfolioChangeCommand(1001, "ZZZ", unknown, 1, 10m)));

        Assert.Equal("Неизвестная операция над портфелем", ex.Message);
    }
}
