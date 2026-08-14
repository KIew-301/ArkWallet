using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineSlotCalculationServiceTest
{
    private static MiningMachineSlotCalculationService CreateService(ArkWalletDbContext db) =>
        new(db, new MiningEngine(), NullLogger<MiningMachineSlotCalculationService>.Instance);

    private static async Task<(MiningMachine machine, MiningMachineRule machineRule)> CreateActiveSlotAsync(
        ArkWalletDbContext db,
        long traderId,
        string symbol = "AAA",
        decimal globalCoefficient = 4m,
        decimal machineCoefficient = 2m,
        decimal BaseTokenMiningSpeed = 2m,
        decimal machineEfficiency = 1m,
        string machineName = "SM-01")
    {
        var tokenResult = await HelpMethods.CreateToken(db, symbol);
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);

        var machine = MiningMachine.Create(
            machineName, MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz", machineEfficiency);
        var machineRule = MiningMachineRule.Create(0, symbol, machineCoefficient);
        machine.MiningMachineRules.Add(machineRule);
        db.MiningMachines.Add(machine);

        var globalRule = MiningGlobalRule.Create(symbol, globalCoefficient, globalCoefficient, BaseTokenMiningSpeed);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(traderId, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.SwitchTargetToken(traderId, symbol, machineRule.Id, globalRule.Id, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.CompleteSwitching();
        db.MiningMachineSlots.Add(slot);

        await db.SaveChangesAsync();
        return (machine, machineRule);
    }

    [Fact]
    public async Task TakeTokensOnMachinesAsync_MultipliesCoefficientsAndTiming()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateActiveSlotAsync(db, 111);

        var result = await CreateService(db).TakeTokensOnMachinesAsync(3m);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var processed));
        Assert.Equal(1, processed);

        var slot = await db.MiningMachineSlots.SingleAsync();
        Assert.Equal(48m, slot.TokensAmountCollected);
    }

    [Fact]
    public async Task TakeTokensOnMachinesAsync_MachineEfficiency_MultipliesTokens()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateActiveSlotAsync(db, 111, machineEfficiency: 1.5m);

        var result = await CreateService(db).TakeTokensOnMachinesAsync(3m);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var processed));
        Assert.Equal(1, processed);

        var slot = await db.MiningMachineSlots.SingleAsync();
        Assert.Equal(72m, slot.TokensAmountCollected);
    }

    [Fact]
    public async Task TakeTokensOnMachinesAsync_MultipleSlots_AccumulatesTokens()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateActiveSlotAsync(db, 111, "AAA", machineName: "SM-01");
        await CreateActiveSlotAsync(db, 111, "BBB", machineName: "SM-02");

        var result = await CreateService(db).TakeTokensOnMachinesAsync(1m);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var processed));
        Assert.Equal(2, processed);
    }

    [Fact]
    public async Task TakeTokensOnMachinesAsync_NonPositiveTimingCoeff_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).TakeTokensOnMachinesAsync(0m);

        Assert.False(result.IsSuccess);
        Assert.Contains("больше нуля", result.Message);

        var negative = await CreateService(db).TakeTokensOnMachinesAsync(-2m);
        Assert.False(negative.IsSuccess);
    }

    [Fact]
    public async Task TakeTokensOnMachinesAsync_DefaultTimingCoeff_Accumulates()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateActiveSlotAsync(db, 111);

        var result = await CreateService(db).TakeTokensOnMachinesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(16m, (await db.MiningMachineSlots.SingleAsync()).TokensAmountCollected);
    }

    [Fact]
    public async Task TakeTokensOnMachinesAsync_PassiveAndSwitchingSlots_AreSkipped()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var tokenResult = await HelpMethods.CreateToken(db, "AAA");
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);
        var machine = MiningMachine.Create("SM-01", MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz", 1m);
        var machineRule = MiningMachineRule.Create(0, "AAA", 2m);
        machine.MiningMachineRules.Add(machineRule);
        db.MiningMachines.Add(machine);
        var globalRule = MiningGlobalRule.Create("AAA", 4m, 4m, 2m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var passive = MiningMachineSlot.Create(traderId: 111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var switching = MiningMachineSlot.Create(traderId: 111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        switching.SwitchTargetToken(111, "AAA", machineRule.Id, globalRule.Id, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.AddRange(passive, switching);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeTokensOnMachinesAsync(3m);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var processed));
        Assert.Equal(0, processed);
        Assert.Equal(0m, passive.TokensAmountCollected);
        Assert.Equal(0m, switching.TokensAmountCollected);
    }
}
