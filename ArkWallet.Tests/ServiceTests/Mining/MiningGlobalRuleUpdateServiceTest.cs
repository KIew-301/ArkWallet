using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningGlobalRuleUpdateServiceTest
{
    private static async Task CreateGlobalRule(ArkWalletDbContext db, string symbol = "ZZZ")
    {
        await HelpMethods.CreateToken(db, symbol);
        db.MiningGlobalRules.Add(MiningGlobalRule.Create(symbol, 1m, 1.2m, 50m));
        await db.SaveChangesAsync();
    }

    private static MiningGlobalRuleUpdateService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<MiningGlobalRuleUpdateService>.Instance);

    [Fact]
    public async Task UpdateRuleAsync_EmptySymbol_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).UpdateRuleAsync("  ", 1m, 1m, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("Требуется символ токена", result.Message);
    }

    [Fact]
    public async Task UpdateRuleAsync_NoParams_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateGlobalRule(db);

        var result = await CreateService(db).UpdateRuleAsync("ZZZ", null, null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("Не указаны параметры для обновления", result.Message);
    }

    [Fact]
    public async Task UpdateRuleAsync_OnlyOneCoefficient_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateGlobalRule(db);

        var result = await CreateService(db).UpdateRuleAsync("ZZZ", 1.5m, null, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("парой", result.Message);
    }

    [Fact]
    public async Task UpdateRuleAsync_RuleNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "ZZZ");

        var result = await CreateService(db).UpdateRuleAsync("ZZZ", 1.5m, 1.3m, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("не найдено", result.Message);
    }

    [Fact]
    public async Task UpdateRuleAsync_UpdateCoefficients_Updates()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateGlobalRule(db);

        var result = await CreateService(db).UpdateRuleAsync("ZZZ", 1.5m, 1.3m, null);

        Assert.True(result.IsSuccess, result.Message);
        var rule = await db.MiningGlobalRules.FirstAsync(r => r.TokenId == "ZZZ");
        Assert.Equal(1.5m, rule.CurrentCoefficient);
        Assert.Equal(1.3m, rule.FutureCoefficient);
        Assert.Equal(50m, rule.BaseMiningSpeed);
    }

    [Fact]
    public async Task UpdateRuleAsync_UpdateBaseMiningSpeed_Updates()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateGlobalRule(db);

        var result = await CreateService(db).UpdateRuleAsync("ZZZ", null, null, 75m);

        Assert.True(result.IsSuccess, result.Message);
        var rule = await db.MiningGlobalRules.FirstAsync(r => r.TokenId == "ZZZ");
        Assert.Equal(1m, rule.CurrentCoefficient);
        Assert.Equal(1.2m, rule.FutureCoefficient);
        Assert.Equal(75m, rule.BaseMiningSpeed);
    }

    [Fact]
    public async Task UpdateRuleAsync_UpdateBoth_Updates()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateGlobalRule(db);

        var result = await CreateService(db).UpdateRuleAsync("ZZZ", 2m, 1.8m, 100m);

        Assert.True(result.IsSuccess, result.Message);
        var rule = await db.MiningGlobalRules.FirstAsync(r => r.TokenId == "ZZZ");
        Assert.Equal(2m, rule.CurrentCoefficient);
        Assert.Equal(1.8m, rule.FutureCoefficient);
        Assert.Equal(100m, rule.BaseMiningSpeed);
    }
}
