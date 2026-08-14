using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineQueryServiceTest
{
    private static MiningMachineQueryService CreateService(ArkWalletDbContext db) =>
        new(db, new MiningEngine(), NullLogger<MiningMachineQueryService>.Instance);

    private static async Task<CharacterToken> CreateTokenAsync(
        ArkWalletDbContext db, string symbol, decimal price = 100)
    {
        var result = await HelpMethods.CreateToken(db, symbol, price: price);
        Assert.True(result.IsSuccess, result.Message);
        return await db.CharacterTokens.SingleAsync(t => t.Symbol == symbol);
    }

    private static MiningMachine CreateMachine(
        string name,
        decimal cost,
        bool isActiveForSale = true,
        params (string Symbol, decimal Coefficient)[] rules)
    {
        var machine = MiningMachine.Create(
            name, MiningMachineType.SMAI, 10, 80, isActiveForSale, cost, "img.zzz", 1m);
        foreach (var (symbol, coefficient) in rules)
            machine.MiningMachineRules.Add(MiningMachineRule.Create(0, symbol, coefficient));
        return machine;
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_ReturnsMachineWithAllFields()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);

        var globalRule = MiningGlobalRule.Create("AAA", 4m, 4m, 2m);
        db.MiningGlobalRules.Add(globalRule);

        var machine = CreateMachine("SM-01", 1000, rules: ("AAA", 1m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal(machine.Id, data.Id);
        Assert.Equal("SM-01", data.Name);
        Assert.Equal("SMAI", data.Type);
        Assert.Equal(10, data.SwitchingTime);
        Assert.Equal(80m, data.Reusability);
        Assert.Equal(1000m, data.Cost);

        var tokenData = Assert.Single(data.EffectiveTokensMiningData);
        Assert.Equal("AAA", tokenData.Symbol);
        Assert.Equal(8m, tokenData.MiningSpeed);
        Assert.Equal(800m, tokenData.Profit);
        Assert.Empty(data.StableTokensMiningData);
        Assert.Equal(800m, data.MaxProfit);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_SplitsTokensByCoefficient_AndMaxProfitAcrossGroups()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        await CreateTokenAsync(db, "BBB", price: 50);
        await CreateTokenAsync(db, "CCC", price: 100);

        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 4m, 4m, 2m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("BBB", 1m, 1m, 1m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("CCC", 4m, 4m, 2m));

        var machine = CreateMachine("SM-01", 1000, true,
            ("AAA", 1m), ("BBB", 0.8m), ("CCC", 2m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);

        var effective = Assert.Single(data.EffectiveTokensMiningData);
        Assert.Equal("AAA", effective.Symbol);
        Assert.Equal(800m, effective.Profit);

        var stable = Assert.Single(data.StableTokensMiningData);
        Assert.Equal("BBB", stable.Symbol);
        Assert.Equal(40m, stable.Profit);

        Assert.DoesNotContain(data.EffectiveTokensMiningData, d => d.Symbol == "CCC");
        Assert.DoesNotContain(data.StableTokensMiningData, d => d.Symbol == "CCC");
        Assert.Equal(800m, data.MaxProfit);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_SortedByCostAscending()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.MiningMachines.AddRange(
            CreateMachine("EXP", 500),
            CreateMachine("BUD", 300),
            CreateMachine("VIP", 700));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        Assert.Equal(new[] { "BUD", "EXP", "VIP" }, machines.Select(m => m.Name).ToArray());
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_NotForSaleMachines_AreExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.MiningMachines.Add(CreateMachine("INACTIVE", 100, isActiveForSale: false));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        Assert.Empty(machines);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_MachineWithoutRules_ReturnsEmptyTokenData()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.MiningMachines.Add(CreateMachine("SM-01", 1000));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Empty(data.EffectiveTokensMiningData);
        Assert.Empty(data.StableTokensMiningData);
        Assert.Equal(0m, data.MaxProfit);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_NoMachines_ReturnsEmptyList()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        Assert.Empty(machines);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_OwnedMachines_AreExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        db.MiningMachines.AddRange(
            CreateMachine("OWNED", 100),
            CreateMachine("FREE", 200));
        await db.SaveChangesAsync();

        var owned = await db.MiningMachines.SingleAsync(m => m.Name == "OWNED");
        db.MiningMachineSlots.Add(MiningMachineSlot.Create(
            111, owned.Id, 80, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal("FREE", data.Name);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_SoldMachine_IsNotExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        db.MiningMachines.Add(CreateMachine("SOLD", 100));
        await db.SaveChangesAsync();

        var machine = await db.MiningMachines.SingleAsync(m => m.Name == "SOLD");
        var slot = MiningMachineSlot.Create(
            111, machine.Id, 80, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.Sell(111, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal("SOLD", data.Name);
    }
}
