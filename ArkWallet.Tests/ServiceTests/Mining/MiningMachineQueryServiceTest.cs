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
        decimal efficiency = 1m,
        bool isActiveForSale = true,
        params (string Symbol, decimal Coefficient)[] rules)
    {
        var machine = MiningMachine.Create(
            MiningMachineType.SMAI, 10, 80, isActiveForSale, "img.zzz", efficiency);
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

        var machine = CreateMachine(rules: ("AAA", 1m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal(machine.Id, data.Id);
        Assert.Equal(machine.Name, data.Name);
        Assert.Equal("SMAI", data.Type);
        Assert.Equal(10, data.SwitchingTime);
        Assert.Equal(80m, data.Reusability);
        Assert.Equal(machine.Cost, data.Cost);

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

        var machine = CreateMachine(1m, true,
            ("AAA", 1m), ("BBB", 0.8m));
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

        Assert.Equal(800m, data.MaxProfit);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_SortedByCostAscending()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var expensive = CreateMachine(1m);
        var cheap = CreateMachine(0.1m);
        var medium = CreateMachine(0.5m);
        db.MiningMachines.AddRange(cheap, medium, expensive);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(0);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var expectedOrder = new[] { cheap, medium, expensive }
            .OrderBy(m => m.Cost)
            .Select(m => m.Id)
            .ToArray();
        Assert.Equal(expectedOrder, machines.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_NotForSaleMachines_AreExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.MiningMachines.Add(CreateMachine(isActiveForSale: false));
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
        db.MiningMachines.Add(CreateMachine());
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

        var owned = CreateMachine(0.1m);
        var free = CreateMachine(0.5m);
        db.MiningMachines.AddRange(owned, free);
        await db.SaveChangesAsync();

        db.MiningMachineSlots.Add(MiningMachineSlot.Create(
            111, owned, 80, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal(free.Id, data.Id);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_SoldMachine_IsNotExcluded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var machine = CreateMachine(0.1m);
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(
            111, machine, 80, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.Sell(111, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync(111);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal(machine.Id, data.Id);
    }
}
