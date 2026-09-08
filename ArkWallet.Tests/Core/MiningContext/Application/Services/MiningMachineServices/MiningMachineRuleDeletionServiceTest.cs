using ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.Core.MiningContext.Application.Services.MiningMachineServices;

public class MiningMachineRuleDeletionServiceTest
{
    private static async Task<(long MachineId, long RuleId)> CreateMachineWithRule(ArkWalletDbContext db, string symbol = "ZZZ")
    {
        await HelpMethods.CreateToken(db, symbol);
        var machine = MiningMachine.Create(MiningMachineType.SMAI, 10, 50, true, "img.zzz", 1m);
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var rule = MiningMachineRule.Create(machine.Id, symbol, 0.9m);
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
    public async Task DeleteRuleAsync_RuleCopiedToSlot_DeletesCatalogRuleKeepsSlotRule()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 1001);
        var (machineId, ruleId) = await CreateMachineWithRule(db);

        var globalRule = MiningGlobalRule.Create("ZZZ", 1m, 1.2m, 50m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var machine = await db.MiningMachines
            .Include(m => m.MiningMachineRules)
            .SingleAsync(m => m.Id == machineId);

        var slot = MiningMachineSlot.Create(1001, machine, 500m, DateTime.UtcNow);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MiningMachineRuleDeletionService(db, NullLogger<MiningMachineRuleDeletionService>.Instance);

        var result = await service.DeleteRuleAsync(ruleId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.MiningMachineRules.FindAsync(ruleId));
        var slotRule = await db.MiningMachineSlotRules
            .SingleAsync(r => r.MiningMachineSlotId == slot.Id);
        Assert.Equal("ZZZ", slotRule.CharacterTokenId);
        Assert.Equal(0.9m, slotRule.MiningCoefficient);
    }
}
