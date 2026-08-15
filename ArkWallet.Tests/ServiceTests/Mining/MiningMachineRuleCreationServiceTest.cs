using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineRuleCreationServiceTest
{
    private static MiningMachineRuleCreationService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<MiningMachineRuleCreationService>.Instance);

    private static async Task<MiningMachine> CreateMachineAsync(ArkWalletDbContext db)
    {
        var machine = MiningMachine.Create(
            MiningMachineType.SMAI, 10, 80, true, "img.zzz", 1m);
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        return machine;
    }

    private static async Task CreateTokenAsync(ArkWalletDbContext db, string symbol)
    {
        var result = await HelpMethods.CreateToken(db, symbol);
        Assert.True(result.IsSuccess, result.Message);
    }

    private static MiningMachineRuleCreationCommand BuildCommand(
        long miningMachineId = 0, string symbol = "", decimal coefficient = 1m) =>
        new(miningMachineId, symbol, coefficient);

    [Fact]
    public async Task CreateRuleAsync_ValidData_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);
        await CreateTokenAsync(db, "AAA");

        var result = await CreateService(db).CreateRuleAsync(
            BuildCommand(machine.Id, "AAA", 0.9m));

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var ruleId));
        Assert.True(ruleId > 0);
        var rule = await db.MiningMachineRules.FindAsync(ruleId);
        Assert.Equal(machine.Id, rule!.MiningMachineId);
        Assert.Equal("AAA", rule.CharacterTokenId);
        Assert.Equal(0.9m, rule.MiningCoefficient);
    }

    [Fact]
    public async Task CreateRuleAsync_DuplicateRule_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);
        await CreateTokenAsync(db, "AAA");

        var service = CreateService(db);
        var first = await service.CreateRuleAsync(BuildCommand(machine.Id, "AAA", 1m));

        var result = await service.CreateRuleAsync(BuildCommand(machine.Id, "AAA", 0.8m));

        Assert.True(first.IsSuccess, first.Message);
        Assert.False(result.IsSuccess);
        Assert.Contains("уже существует", result.Message);
        Assert.Single(await db.MiningMachineRules.Where(r => r.MiningMachineId == machine.Id).ToListAsync());
    }

    [Fact]
    public async Task CreateRuleAsync_MoreThanTenRules_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);
        var service = CreateService(db);

        for (var i = 0; i < 10; i++)
        {
            var symbol = "TK" + i.ToString("00");
            await CreateTokenAsync(db, symbol);
            var result = await service.CreateRuleAsync(BuildCommand(machine.Id, symbol, 1m));
            Assert.True(result.IsSuccess, $"Rule #{i}: {result.Message}");
        }

        await CreateTokenAsync(db, "ZZZ");
        var extra = await service.CreateRuleAsync(BuildCommand(machine.Id, "ZZZ", 1m));

        Assert.False(extra.IsSuccess);
        Assert.Contains("10", extra.Message);
        Assert.Equal(10, await db.MiningMachineRules.CountAsync(r => r.MiningMachineId == machine.Id));
    }

    [Fact]
    public async Task CreateRuleAsync_InvalidCoefficient_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);
        await CreateTokenAsync(db, "AAA");

        var result = await CreateService(db).CreateRuleAsync(
            BuildCommand(machine.Id, "AAA", 0m));

        Assert.False(result.IsSuccess);
        Assert.Contains("от 0,65 до 1", result.Message);
    }

    [Fact]
    public async Task CreateRuleAsync_MachineNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA");

        var result = await CreateService(db).CreateRuleAsync(
            BuildCommand(999, "AAA", 1m));

        Assert.False(result.IsSuccess);
        Assert.Contains("не существует", result.Message);
    }

    [Fact]
    public async Task CreateRuleAsync_TokenNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);

        var result = await CreateService(db).CreateRuleAsync(
            BuildCommand(machine.Id, "UNKNOWN", 1m));

        Assert.False(result.IsSuccess);
        Assert.Contains("не существует", result.Message);
    }

    [Fact]
    public async Task CreateRulesAsync_BulkCreation_ReturnsSuccess()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);
        await CreateTokenAsync(db, "AAA");
        await CreateTokenAsync(db, "BBB");

        var result = await CreateService(db).CreateRulesAsync(
            new[]
            {
                BuildCommand(machine.Id, "AAA", 1m),
                BuildCommand(machine.Id, "BBB", 0.8m)
            });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, await db.MiningMachineRules.CountAsync(r => r.MiningMachineId == machine.Id));
    }

    [Fact]
    public async Task CreateRulesAsync_OneDuplicate_FailsWholeBatch()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = await CreateMachineAsync(db);
        await CreateTokenAsync(db, "AAA");
        await CreateTokenAsync(db, "BBB");
        var service = CreateService(db);

        var first = await service.CreateRuleAsync(BuildCommand(machine.Id, "AAA", 1m));

        var result = await service.CreateRulesAsync(
            new[]
            {
                BuildCommand(machine.Id, "BBB", 1m),
                BuildCommand(machine.Id, "AAA", 0.8m)
            });

        Assert.True(first.IsSuccess, first.Message);
        Assert.False(result.IsSuccess);
        Assert.Single(await db.MiningMachineRules.Where(r => r.MiningMachineId == machine.Id).ToListAsync());
    }
}
