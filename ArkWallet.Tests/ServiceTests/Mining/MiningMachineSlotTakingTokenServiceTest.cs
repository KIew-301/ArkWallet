using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineSlotTakingTokenServiceTest
{
    private static MiningMachineSlotTakingTokenService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<MiningMachineSlotTakingTokenService>.Instance);

    private static async Task<MiningMachineSlot> CreateSlotAsync(
        ArkWalletDbContext db, long traderId, long? ownerId = null, decimal collected = 0)
    {
        var machine = MiningMachine.Create(
            "SM-01", MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz");
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(
            ownerId ?? traderId, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        if (collected > 0)
            slot.AddTokens(collected);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot;
    }

    private static async Task MakeSlotActiveAsync(ArkWalletDbContext db, MiningMachineSlot slot, string symbol)
    {
        var tokenResult = await HelpMethods.CreateToken(db, symbol);
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);

        var machineRule = MiningMachineRule.Create(slot.MiningMachineId, symbol, 1.5m);
        db.MiningMachineRules.Add(machineRule);
        var globalRule = MiningGlobalRule.Create(symbol, 1m, 1m, 1m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        slot.SwitchTargetToken(slot.TraderId, symbol, machineRule.Id, globalRule.Id, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.CompleteSwitching();
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_CollectsWholeTokens()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slot = await CreateSlotAsync(db, 111, collected: 3.75m);
        await MakeSlotActiveAsync(db, slot, "AAA");

        var result = await CreateService(db).TakeTokensFromMachineAsync(111, slot.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("AAA", data.Symbol);
        Assert.Equal(3, data.TokensCollected);

        var updated = await db.MiningMachineSlots.FindAsync(slot.Id);
        Assert.Equal(0.75m, updated!.TokensAmountCollected);
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_NoToken_ReturnsEmptyCollection()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slot = await CreateSlotAsync(db, 111);

        var result = await CreateService(db).TakeTokensFromMachineAsync(111, slot.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(string.Empty, data.Symbol);
        Assert.Equal(0, data.TokensCollected);
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slot = await CreateSlotAsync(db, 111);

        var result = await CreateService(db).TakeTokensFromMachineAsync(999, slot.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Трейдера не существует", result.Message);
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_SlotNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var result = await CreateService(db).TakeTokensFromMachineAsync(111, 999);

        Assert.False(result.IsSuccess);
        Assert.Contains("Слота не существует", result.Message);
    }

    [Fact]
    public async Task TakeTokensFromMachineAsync_NotOwner_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        var owner = await HelpMethods.RegisterTrader(db, 222);
        Assert.True(owner.IsSuccess, owner.Message);

        var slot = await CreateSlotAsync(db, 111, ownerId: 222, collected: 5m);

        var result = await CreateService(db).TakeTokensFromMachineAsync(111, slot.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("не владеет", result.Message);
        Assert.Equal(5m, slot.TokensAmountCollected);
    }

    [Fact]
    public async Task TakeTokensFromMachinesAsync_CollectsFromAllSlots()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slotA = await CreateSlotAsync(db, 111, collected: 2.5m);
        var slotB = await CreateSlotAsync(db, 111, collected: 1.25m);
        await MakeSlotActiveAsync(db, slotA, "AAA");
        await MakeSlotActiveAsync(db, slotB, "BBB");

        var result = await CreateService(db).TakeTokensFromMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var results));
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Symbol == "AAA" && r.TokensCollected == 2);
        Assert.Contains(results, r => r.Symbol == "BBB" && r.TokensCollected == 1);

        Assert.Equal(0.5m, (await db.MiningMachineSlots.FindAsync(slotA.Id))!.TokensAmountCollected);
        Assert.Equal(0.25m, (await db.MiningMachineSlots.FindAsync(slotB.Id))!.TokensAmountCollected);
    }

    [Fact]
    public async Task TakeTokensFromMachinesAsync_NoWholeTokens_ReturnsEmptyList()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slot = await CreateSlotAsync(db, 111, collected: 0.5m);
        await MakeSlotActiveAsync(db, slot, "AAA");

        var result = await CreateService(db).TakeTokensFromMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var results));
        Assert.Empty(results);
    }

    [Fact]
    public async Task TakeTokensFromMachinesAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).TakeTokensFromMachinesAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Contains("Трейдера не существует", result.Message);
    }
}
