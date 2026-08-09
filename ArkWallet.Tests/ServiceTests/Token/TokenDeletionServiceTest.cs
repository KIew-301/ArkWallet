using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Token;

public class TokenDeletionServiceTest
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteTokenAsync_EmptySymbol_ReturnsFail(string symbol)
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeleteTokenAsync(symbol);

        Assert.False(result.IsSuccess);
        Assert.Equal("Требуется символ токена", result.Message);
    }

    [Fact]
    public async Task DeleteTokenAsync_TokenNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeleteTokenAsync("NONEXISTENT");

        Assert.False(result.IsSuccess);
        Assert.Contains("не найден", result.Message);
    }

    [Fact]
    public async Task DeleteTokenAsync_ExistingToken_DeletesTokenAndRelatedData()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.CreatePriceCandle(db, "ZZZ", 100m, DateTime.UtcNow);
        await HelpMethods.GiveMoney(db, 1001, 100000);
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 5);

        var orderResult = await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 10, 90);
        Assert.True(orderResult.IsSuccess);

        db.MarketMakerBots.Add(MarketMakerBot.Create(1001, "ZZZ", BotRole.Buyer, 50));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeleteTokenAsync("ZZZ");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.CharacterTokens.FindAsync("ZZZ"));
        Assert.Empty(await db.PortfolioItems.Where(p => p.CharacterTokenId == "ZZZ").ToListAsync());
        Assert.Empty(await db.TradeOrders.Where(o => o.CharacterTokenId == "ZZZ").ToListAsync());
        Assert.Empty(await db.Trades.Where(t => t.CharacterTokenId == "ZZZ").ToListAsync());
        Assert.Empty(await db.PriceCandles.Where(c => c.CharacterTokenId == "ZZZ").ToListAsync());
        Assert.Empty(await db.MarketMakerBots.Where(b => b.Symbol == "ZZZ").ToListAsync());
    }

    [Fact]
    public async Task DeactivateTokenAsync_EmptySymbol_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeactivateTokenAsync("");

        Assert.False(result.IsSuccess);
        Assert.Equal("Требуется символ токена", result.Message);
    }

    [Fact]
    public async Task DeactivateTokenAsync_TokenNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeactivateTokenAsync("NONEXISTENT");

        Assert.False(result.IsSuccess);
        Assert.Contains("не найден", result.Message);
    }

    [Fact]
    public async Task DeleteTokenAsync_MixedCaseSymbol_DeletesToken()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "Loony");
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeleteTokenAsync("loony");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.CharacterTokens.FindAsync("Loony"));
    }

    [Fact]
    public async Task DeactivateTokenAsync_MixedCaseSymbol_SetsIsActiveFalse()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "Loony");
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeactivateTokenAsync("LOONY");

        Assert.True(result.IsSuccess, result.Message);
        var token = await db.CharacterTokens.FindAsync("Loony");
        Assert.NotNull(token);
        Assert.False(token!.IsActive);
    }

    [Fact]
    public async Task DeactivateTokenAsync_ExistingToken_SetsIsActiveFalse()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "ZZZ");
        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeactivateTokenAsync("ZZZ");

        Assert.True(result.IsSuccess);
        var token = await db.CharacterTokens.FindAsync("ZZZ");
        Assert.NotNull(token);
        Assert.False(token!.IsActive);
        Assert.False(token.CanBeTraded());
    }

    [Fact]
    public async Task DeactivateTokenAsync_ExistingToken_DeletesBots()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "ZZZ");
        db.MarketMakerBots.Add(MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 50));
        db.MarketMakerBots.Add(MarketMakerBot.Create(102, "ZZZ", BotRole.Seller, 50));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeactivateTokenAsync("ZZZ");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(await db.MarketMakerBots.Where(b => b.Symbol == "ZZZ").ToListAsync());
    }

    [Fact]
    public async Task DeleteTokenAsync_MixedCaseSymbol_DeletesBots()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "Loony");
        db.MarketMakerBots.Add(MarketMakerBot.Create(101, "Loony", BotRole.Buyer, 50));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new TokenDeletionService(db, NullLogger<TokenDeletionService>.Instance);

        var result = await service.DeleteTokenAsync("loony");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(await db.MarketMakerBots.Where(b => b.Symbol == "Loony").ToListAsync());
    }
}
