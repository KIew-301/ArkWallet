using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningGlobalRuleCreationServiceTest
{
    private static MiningGlobalRuleCreationService CreateService(ArkWalletDbContext db) =>
        new(db, new MiningEngine(), NullLogger<MiningGlobalRuleCreationService>.Instance);

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
    public async Task CreateRulesAsync_NoRules_CreatesRulesForAllActiveTokens()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        await CreateTokenAsync(db, "BBB", price: 50);

        var result = await CreateService(db).CreateRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, await db.MiningGlobalRules.CountAsync());

        var rules = await db.MiningGlobalRules.ToListAsync();
        foreach (var rule in rules)
        {
            Assert.InRange(rule.CurrentCoefficient, MiningEngine.MinCoefficient, MiningEngine.MaxCoefficient);
            Assert.InRange(rule.FutureCoefficient, MiningEngine.MinCoefficient, MiningEngine.MaxCoefficient);
        }

        var aaa = rules.Single(r => r.TokenId == "AAA");
        Assert.Equal(0.5m, aaa.BaseTokenMiningSpeed);

        var bbb = rules.Single(r => r.TokenId == "BBB");
        Assert.Equal(1m, bbb.BaseTokenMiningSpeed);
    }

    [Fact]
    public async Task CreateRulesAsync_ExistingRule_AdvancesCoefficients()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 0.9m, 1.1m, 2m));
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateRulesAsync();

        Assert.True(result.IsSuccess, result.Message);

        var rule = await db.MiningGlobalRules.SingleAsync();
        Assert.Equal(1.1m, rule.CurrentCoefficient);
        Assert.InRange(rule.FutureCoefficient, MiningEngine.MinCoefficient, MiningEngine.MaxCoefficient);
        Assert.Equal(0.5m, rule.BaseTokenMiningSpeed);
    }

    [Fact]
    public async Task CreateRulesAsync_ZeroPriceTokens_AreSkipped()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "VALID", price: 100);
        await CreateTokenAsync(db, "FREE", price: 100);
        var freeToken = await db.CharacterTokens.SingleAsync(t => t.Symbol == "FREE");
        freeToken.UpdatePrice(0);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        var rule = await db.MiningGlobalRules.SingleAsync();
        Assert.Equal("VALID", rule.TokenId);
    }

    [Fact]
    public async Task CreateRulesAsync_InactiveTokens_AreSkipped()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "ACTIVE", price: 100);
        await CreateTokenAsync(db, "INACTIVE", price: 100, isActive: false);

        var result = await CreateService(db).CreateRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        var rule = await db.MiningGlobalRules.SingleAsync();
        Assert.Equal("ACTIVE", rule.TokenId);
    }

    [Fact]
    public async Task CreateRulesAsync_NoTokens_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).CreateRulesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(await db.MiningGlobalRules.ToListAsync());
    }
}
