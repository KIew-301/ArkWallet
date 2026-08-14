using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineSlotSwitchingServiceTest
{
    private static MiningMachineSlotSwitchingService CreateService(
        ArkWalletDbContext db, TestTimeProvider? timeProvider = null) =>
        new(db, NullLogger<MiningMachineSlotSwitchingService>.Instance, timeProvider);

    private static async Task<(MiningMachine machine, CharacterToken token)> CreateMachineWithRuleAsync(
        ArkWalletDbContext db, string symbol = "AAA", string machineName = "SM-01")
    {
        var tokenResult = await HelpMethods.CreateToken(db, symbol);
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);
        var token = await db.CharacterTokens.SingleAsync(t => t.Symbol == symbol);

        var machine = MiningMachine.Create(
            machineName, MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz", 1m);
        machine.MiningMachineRules.Add(MiningMachineRule.Create(0, symbol, 1.5m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        return (machine, token);
    }

    private static async Task<MiningGlobalRule> CreateGlobalRuleAsync(ArkWalletDbContext db, string symbol)
    {
        var rule = MiningGlobalRule.Create(symbol, 1m, 1.1m, 5m);
        db.MiningGlobalRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    private static async Task<MiningMachineSlot> CreateSlotAsync(
        ArkWalletDbContext db, long traderId, long machineId, long globalRuleId)
    {
        var slot = MiningMachineSlot.Create(traderId, machineId, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot;
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_ValidSwitch_SwitchesSlot()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db);
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");
        var slot = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);

        var timeProvider = new TestTimeProvider();
        var result = await CreateService(db, timeProvider).SwitchTargetTokenAsync(111, slot.Id, "AAA");

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(string.Empty, data.Symbol);
        Assert.Equal(0, data.TokensCollected);

        var updated = await db.MiningMachineSlots.FindAsync(slot.Id);
        Assert.Equal(MiningMachineSlotStatus.Switching, updated!.Status);
        Assert.Equal("AAA", updated.TokenId);
        Assert.NotNull(updated.MachineRuleId);
        Assert.Equal(globalRule.Id, updated.MiningGlobalRuleId);
        Assert.Equal(timeProvider.Now.DateTime, updated.StartSwitchingDateTime);
        Assert.Equal(timeProvider.Now.DateTime.AddMinutes(10), updated.EndSwitchingDateTime);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_WithCollectedTokens_ReturnsWholePartAndClearsIt()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db, machineName: "SM-02");
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");
        var slot = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);
        slot.AddTokens(2.75m);
        var machineRuleId = machine.MiningMachineRules.Single(r => r.CharacterTokenId == "AAA").Id;
        slot.SwitchTargetToken(111, "AAA", machineRuleId, globalRule.Id, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.CompleteSwitching();
        await db.SaveChangesAsync();

        var result = await CreateService(db).SwitchTargetTokenAsync(111, slot.Id, "AAA");

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("AAA", data.Symbol);
        Assert.Equal(2, data.TokensCollected);

        var updated = await db.MiningMachineSlots.FindAsync(slot.Id);
        Assert.Equal(0.75m, updated!.TokensAmountCollected);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db);
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");
        var slot = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);

        var result = await CreateService(db).SwitchTargetTokenAsync(999, slot.Id, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Contains("Трейдера не существует", result.Message);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_TokenNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var tokenResult = await HelpMethods.CreateToken(db, "AAA");
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);

        var machine = MiningMachine.Create("SM-01", MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz", 1m);
        machine.MiningMachineRules.Add(MiningMachineRule.Create(0, "AAA", 1m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");
        var slot = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);

        var result = await CreateService(db).SwitchTargetTokenAsync(111, slot.Id, "UNKNOWN");

        Assert.False(result.IsSuccess);
        Assert.Contains("Токена не существует", result.Message);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_SlotNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await CreateMachineWithRuleAsync(db);

        var result = await CreateService(db).SwitchTargetTokenAsync(111, 999, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Contains("Слота не существует", result.Message);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_MachineRuleNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var tokenResult = await HelpMethods.CreateToken(db, "AAA");
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);

        var machine = MiningMachine.Create("SM-01", MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz", 1m);
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");
        var slot = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);

        var result = await CreateService(db).SwitchTargetTokenAsync(111, slot.Id, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Contains("Правила для такой связки", result.Message);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_GlobalRuleNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db);
        var slot = MiningMachineSlot.Create(111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SwitchTargetTokenAsync(111, slot.Id, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Contains("Глобального правила", result.Message);
    }

    [Fact]
    public async Task SwitchTargetTokenAsync_NotSlotOwner_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        var owner = await HelpMethods.RegisterTrader(db, 222);
        Assert.True(owner.IsSuccess, owner.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db);
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");
        var slot = await CreateSlotAsync(db, 222, machine.Id, globalRule.Id);

        var result = await CreateService(db).SwitchTargetTokenAsync(111, slot.Id, "AAA");

        Assert.False(result.IsSuccess);
        Assert.Contains("не владеет", result.Message);
    }

    [Fact]
    public async Task CheckSwitchingAsync_ExpiredSwitches_AreCompleted()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db);
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");

        var timeProvider = new TestTimeProvider();
        var service = CreateService(db, timeProvider);

        var slot1 = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);
        var slot2 = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);
        await service.SwitchTargetTokenAsync(111, slot1.Id, "AAA");
        await service.SwitchTargetTokenAsync(111, slot2.Id, "AAA");

        timeProvider.SkipInSeconds(11 * 60);

        var result = await service.CheckSwitchingAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var completed));
        Assert.Equal(2, completed);

        Assert.Equal(
            2,
            await db.MiningMachineSlots.CountAsync(s => s.Status == MiningMachineSlotStatus.Active));
    }

    [Fact]
    public async Task CheckSwitchingAsync_IncompleteSwitches_RemainSwitching()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var (machine, _) = await CreateMachineWithRuleAsync(db);
        var globalRule = await CreateGlobalRuleAsync(db, "AAA");

        var timeProvider = new TestTimeProvider();
        var service = CreateService(db, timeProvider);

        var slot = await CreateSlotAsync(db, 111, machine.Id, globalRule.Id);
        await service.SwitchTargetTokenAsync(111, slot.Id, "AAA");

        timeProvider.SkipInSeconds(5 * 60);

        var result = await service.CheckSwitchingAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var completed));
        Assert.Equal(0, completed);

        var updated = await db.MiningMachineSlots.FindAsync(slot.Id);
        Assert.Equal(MiningMachineSlotStatus.Switching, updated!.Status);
    }
}
