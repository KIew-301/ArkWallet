using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineRuleDeletionServiceTest
{
    private static async Task<(long MachineId, long RuleId)> CreateMachineWithRule(ArkWalletDbContext db, string symbol = "ZZZ")
    {
        await HelpMethods.CreateToken(db, symbol);
        var machine = MiningMachine.Create("SM-01", MiningMachineType.SMAI, 10, 50, true, 1000, "img.zzz", 1m);
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var rule = MiningMachineRule.Create(machine.Id, symbol, 1.5m);
        db.MiningMachineRules.Add(rule);
        await db.SaveChangesAsync();

        return (machine.Id, rule.Id);
    }

    [Fact]
    public async Task DeleteRuleAsync_RuleNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new MiningMachineRuleDeletionService(db, NullLogger<MiningMachineRuleDeletionService>.Instance);

        var result = await service.DeleteRuleAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Contains("не найдено", result.Message);
    }

    [Fact]
    public async Task DeleteRuleAsync_ExistingRule_DeletesRule()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var (_, ruleId) = await CreateMachineWithRule(db);
        db.ChangeTracker.Clear();
        var service = new MiningMachineRuleDeletionService(db, NullLogger<MiningMachineRuleDeletionService>.Instance);

        var result = await service.DeleteRuleAsync(ruleId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.MiningMachineRules.FindAsync(ruleId));
    }

    [Fact]
    public async Task DeleteRuleAsync_RuleInUseBySlot_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 1001);
        var (machineId, ruleId) = await CreateMachineWithRule(db);

        var globalRule = MiningGlobalRule.Create("ZZZ", 1m, 1.2m, 50m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(1001, machineId, 500m, DateTime.UtcNow);
        slot.SwitchTargetToken(1001, "ZZZ", ruleId, globalRule.Id, 10, DateTime.UtcNow);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MiningMachineRuleDeletionService(db, NullLogger<MiningMachineRuleDeletionService>.Instance);

        var result = await service.DeleteRuleAsync(ruleId);

        Assert.False(result.IsSuccess);
        Assert.Contains("используемое слотом", result.Message);
        Assert.NotNull(await db.MiningMachineRules.FindAsync(ruleId));
    }
}
