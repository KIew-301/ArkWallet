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
            name, MiningMachineType.SMAI, 10, 80, isActiveForSale, cost, "img.zzz");
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

        var machine = CreateMachine("SM-01", 1000, rules: ("AAA", 2m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Equal(machine.Id, data.Id);
        Assert.Equal("SM-01", data.Name);
        Assert.Equal("SMAI", data.Type);
        Assert.Equal(10, data.SwitchingTime);
        Assert.Equal(80m, data.Reusability);
        Assert.Equal(1000m, data.Cost);

        var tokenData = Assert.Single(data.TokensMiningData);
        Assert.Equal("AAA", tokenData.Symbol);
        Assert.Equal(16m, tokenData.MiningSpeed);
        Assert.Equal(1600m, tokenData.Profit);
        Assert.Equal(1600m, data.MaxProfit);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_MaxProfitIsMaxAcrossTokens_AndSortedByProfitDesc()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await CreateTokenAsync(db, "AAA", price: 100);
        await CreateTokenAsync(db, "BBB", price: 50);

        db.MiningGlobalRules.Add(MiningGlobalRule.Create("AAA", 4m, 4m, 2m));
        db.MiningGlobalRules.Add(MiningGlobalRule.Create("BBB", 1m, 1m, 1m));

        var machine = CreateMachine("SM-01", 1000, true,
            ("AAA", 2m), ("BBB", 10m));
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);

        var bySymbol = data.TokensMiningData.ToDictionary(d => d.Symbol);
        Assert.Equal(500m, bySymbol["BBB"].Profit);
        Assert.Equal(1600m, bySymbol["AAA"].Profit);
        Assert.Equal(1600m, data.MaxProfit);
        Assert.Equal("AAA", data.TokensMiningData[0].Symbol);
        Assert.Equal("BBB", data.TokensMiningData[1].Symbol);
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

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync();

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

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync();

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

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        var data = Assert.Single(machines);
        Assert.Empty(data.TokensMiningData);
        Assert.Equal(0m, data.MaxProfit);
    }

    [Fact]
    public async Task TakeActiveForSaleMachinesAsync_NoMachines_ReturnsEmptyList()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();

        var result = await CreateService(db).TakeActiveForSaleMachinesAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var machines));
        Assert.Empty(machines);
    }
}
