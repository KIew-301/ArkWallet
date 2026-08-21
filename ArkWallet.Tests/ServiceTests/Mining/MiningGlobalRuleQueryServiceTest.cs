using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningGlobalRuleQueryServiceTest
{
    private static readonly string[] ExpectedSymbolsByBaseProfit = ["AAA", "BBB"];

    private static MiningGlobalRuleQueryService CreateService(ArkWalletDbContext db) =>
        new(db, new MiningEngine(), NullLogger<MiningGlobalRuleQueryService>.Instance);

    private static async Task<CharacterToken> CreateTokenAsync(
        ArkWalletDbContext db, string symbol, decimal price = 100, bool isActive = true)
    {
        var result = await HelpMethods.CreateToken(db, symbol, price: price);
        Assert.True(result.IsSuccess, result.Message);
        var token = await db.CharacterTokens.SingleAsync(t => t.Symbol == symbol);
        if (!isActive)
            token.Deactivate();
        await db.SaveChangesAsync();
        return token;
    }

    [Fact]
    public async Task TakeRulesAsync_ReturnsAllFields()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 1m, 1.1m, 2m));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var rules));
        var rule = Assert.Single(rules);
        Assert.Equal("AAA", rule.TokenInfo.Symbol);
        Assert.Equal("Token", rule.TokenInfo.Name);
        Assert.Equal(100m, rule.TokenInfo.CurrentPrice);
        Assert.Equal(2m, rule.BaseTokenMiningSpeed);
        Assert.Equal(200m, rule.BaseProfit);
    }

    [Fact]
    public async Task TakeRulesAsync_TokenWithoutRule_UsesDefaults()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);

        var result = await CreateService(db).TakeRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var rules));
        var rule = Assert.Single(rules);
        Assert.Equal(0m, rule.BaseTokenMiningSpeed);
        Assert.Equal(0m, rule.BaseProfit);
    }

    [Fact]
    public async Task TakeRulesAsync_SortedByBaseProfitDescending()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        await CreateTokenAsync(db, "BBB", price: 50);
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 1m, 1m, 4m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("BBB", 1m, 1m, 1m));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var rules));
        Assert.Equal(ExpectedSymbolsByBaseProfit, rules.Select(r => r.TokenInfo.Symbol).ToArray());
        Assert.Equal(400m, rules[0].BaseProfit);
        Assert.Equal(50m, rules[1].BaseProfit);
    }

    [Fact]
    public async Task TakeRulesAsync_CurrentStatusReflectsRelativeProfit()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        await CreateTokenAsync(db, "BBB", price: 300);
        await CreateTokenAsync(db, "CCC", price: 900);
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 1m, 1m, 1m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("BBB", 1m, 1m, 2m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("CCC", 1m, 1m, 1m));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var rules));
        var bySymbol = rules.ToDictionary(r => r.TokenInfo.Symbol);

        Assert.Equal(nameof(MiningStatus.Unprofitable), bySymbol["AAA"].CurrentMiningStatus);
        Assert.Equal(nameof(MiningStatus.Stable), bySymbol["BBB"].CurrentMiningStatus);
        Assert.Equal(nameof(MiningStatus.Profitable), bySymbol["CCC"].CurrentMiningStatus);
    }

    [Fact]
    public async Task TakeRulesAsync_InactiveTokens_AreExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "ACTIVE", isActive: true);
        await CreateTokenAsync(db, "INACTIVE", isActive: false);

        var result = await CreateService(db).TakeRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var rules));
        var rule = Assert.Single(rules);
        Assert.Equal("ACTIVE", rule.TokenInfo.Symbol);
    }

    [Fact]
    public async Task TakeRulesAsync_NoTokens_ReturnsEmptyList()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).TakeRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var rules));
        Assert.Empty(rules);
    }
}
