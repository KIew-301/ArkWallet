using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineSlotQueryServiceTest
{
    private static MiningMachineSlotQueryService CreateService(
        ArkWalletDbContext db, TestTimeProvider? timeProvider = null) =>
        new(db, new MiningEngine(), NullLogger<MiningMachineSlotQueryService>.Instance, timeProvider);

    private static async Task<MiningMachine> CreateMachineAsync(
        ArkWalletDbContext db, string name = "SM-01", params (string Symbol, decimal Coefficient)[] rules)
    {
        var machine = MiningMachine.Create(
            name, MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz", 1m);
        foreach (var (symbol, coefficient) in rules)
            machine.MiningMachineRules.Add(MiningMachineRule.Create(0, symbol, coefficient));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        return machine;
    }

    private static async Task<CharacterToken> CreateTokenAsync(
        ArkWalletDbContext db, string symbol, decimal price = 100)
    {
        var result = await HelpMethods.CreateToken(db, symbol, price: price);
        Assert.True(result.IsSuccess, result.Message);
        return await db.CharacterTokens.SingleAsync(t => t.Symbol == symbol);
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_ReturnsSlotWithAllFields()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateTokenAsync(db, "AAA", price: 100);
        var machine = await CreateMachineAsync(db, rules: ("AAA", 2m));
        var globalRule = MiningGlobalRule.Create("AAA", 4m, 4m, 2m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();
        var machineRuleId = machine.MiningMachineRules.Single(r => r.CharacterTokenId == "AAA").Id;

        var slot = MiningMachineSlot.Create(
            111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.SwitchTargetToken(111, "AAA", machineRuleId, globalRule.Id, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.CompleteSwitching();
        slot.AddTokens(5.5m);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var timeProvider = new TestTimeProvider();
        var result = await CreateService(db, timeProvider).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        var data = Assert.Single(slots);
        Assert.Equal(slot.Id, data.Id);
        Assert.Equal("SM-01", data.Name);
        Assert.Equal("SMAI", data.Type);
        Assert.Equal("Active", data.Status);
        Assert.Equal(5.5m, data.TokensAmountCollected);
        Assert.Equal(100m, data.SwitchingPercent);
        Assert.Equal(10, data.SwitchingTime);
        Assert.Equal(400m, data.Cost);

        Assert.Equal("AAA", data.ActiveTokenMiningData.Symbol);
        Assert.Equal(16m, data.ActiveTokenMiningData.MiningSpeed);
        Assert.Equal(1600m, data.ActiveTokenMiningData.Profit);
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_SplitsTokensByCoefficient_ActiveTokenInNeitherGroup()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateTokenAsync(db, "AAA", price: 100);
        await CreateTokenAsync(db, "BBB", price: 100);
        await CreateTokenAsync(db, "CCC", price: 100);
        var machine = await CreateMachineAsync(db, name: "SM-01",
            ("AAA", 0.9m), ("BBB", 1m), ("CCC", 0.8m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 4m, 4m, 2m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("BBB", 4m, 4m, 2m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("CCC", 1m, 1m, 1m));
        await db.SaveChangesAsync();
        var machineRuleId = machine.MiningMachineRules.Single(r => r.CharacterTokenId == "AAA").Id;
        var globalRuleId = db.MiningGlobalRules.Single(r => r.TokenId == "AAA").Id;

        var slot = MiningMachineSlot.Create(
            111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.SwitchTargetToken(111, "AAA", machineRuleId, globalRuleId, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.CompleteSwitching();
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        var data = Assert.Single(slots);

        Assert.Equal("AAA", data.ActiveTokenMiningData.Symbol);
        Assert.DoesNotContain(data.EffectiveTokensMiningData, d => d.Symbol == "AAA");
        Assert.DoesNotContain(data.StableTokensMiningData, d => d.Symbol == "AAA");

        var effective = Assert.Single(data.EffectiveTokensMiningData);
        Assert.Equal("BBB", effective.Symbol);

        var stable = Assert.Single(data.StableTokensMiningData);
        Assert.Equal("CCC", stable.Symbol);
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_ActiveTokenEmptyForSlotWithoutToken()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var machine = await CreateMachineAsync(db);
        var slot = MiningMachineSlot.Create(
            111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        var data = Assert.Single(slots);
        Assert.Equal("Passive", data.Status);
        Assert.Equal(string.Empty, data.ActiveTokenMiningData.Symbol);
        Assert.Equal(0m, data.ActiveTokenMiningData.Profit);
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_SwitchingSlot_ShowsSwitchingPercent()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        await CreateTokenAsync(db, "AAA");
        var machine = await CreateMachineAsync(db, rules: ("AAA", 2m));
        var globalRule = MiningGlobalRule.Create("AAA", 4m, 4m, 2m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();
        var machineRuleId = machine.MiningMachineRules.Single(r => r.CharacterTokenId == "AAA").Id;

        var slot = MiningMachineSlot.Create(
            111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.SwitchTargetToken(
            111, "AAA", machineRuleId, globalRule.Id, 10,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(-2));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db, new TestTimeProvider()).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        var data = Assert.Single(slots);
        Assert.Equal("Switching", data.Status);
        Assert.Equal(20m, data.SwitchingPercent);
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_SortedByCreatedAtDescending()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var machine = await CreateMachineAsync(db);
        var older = MiningMachineSlot.Create(111, machine.Id, 400, new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = MiningMachineSlot.Create(111, machine.Id, 500, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.AddRange(older, newer);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        Assert.Equal(new[] { newer.Id, older.Id }, slots.Select(s => s.Id).ToArray());
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_SoldSlots_AreExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var machine = await CreateMachineAsync(db);
        var sold = MiningMachineSlot.Create(111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        sold.Sell(111, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        var active = MiningMachineSlot.Create(111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.AddRange(sold, active);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        var data = Assert.Single(slots);
        Assert.Equal(active.Id, data.Id);
    }

    [Fact]
    public async Task TakeSlotsByTraderAsync_OtherTraderSlots_AreExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        var otherTrader = await HelpMethods.RegisterTrader(db, 222);
        Assert.True(otherTrader.IsSuccess, otherTrader.Message);

        var machine = await CreateMachineAsync(db);
        var otherSlot = MiningMachineSlot.Create(222, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(otherSlot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeSlotsByTraderAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slots));
        Assert.Empty(slots);
    }
}
