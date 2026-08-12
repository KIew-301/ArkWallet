using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineSlotSellingServiceTest
{
    private static MiningMachineSlotSellingService CreateService(
        ArkWalletDbContext db, TestTimeProvider? timeProvider = null) =>
        new(db, NullLogger<MiningMachineSlotSellingService>.Instance, timeProvider);

    private static async Task<MiningMachineSlot> CreateSlotAsync(
        ArkWalletDbContext db, long traderId, long? ownerId = null, decimal cost = 400)
    {
        var machine = MiningMachine.Create(
            "SM-01", MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz");
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(
            ownerId ?? traderId, machine.Id, cost, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot;
    }

    [Fact]
    public async Task SellMachineAsync_ValidSell_CreditsBalanceAndMarksSold()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 100);

        var slot = await CreateSlotAsync(db, 111, cost: 400);
        var timeProvider = new TestTimeProvider();

        var result = await CreateService(db, timeProvider).SellMachineAsync(111, slot.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(string.Empty, data.Symbol);
        Assert.Equal(0, data.TokensCollected);

        var traderEntity = await HelpMethods.GetTrader(db, 111);
        Assert.Equal(1500m, traderEntity!.Balance);

        var updated = await db.MiningMachineSlots.FindAsync(slot.Id);
        Assert.Equal(MiningMachineSlotStatus.Sold, updated!.Status);
        Assert.Equal(timeProvider.Now.DateTime, updated.SoldAt);
    }

    [Fact]
    public async Task SellMachineAsync_WithCollectedTokens_ReturnsWholePart()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var tokenResult = await HelpMethods.CreateToken(db, "AAA");
        Assert.True(tokenResult.IsSuccess, tokenResult.Message);

        var machine = MiningMachine.Create("SM-01", MiningMachineType.SMAI, 10, 80, true, 1000, "img.zzz");
        var machineRule = MiningMachineRule.Create(0, "AAA", 1.5m);
        machine.MiningMachineRules.Add(machineRule);
        db.MiningMachines.Add(machine);

        var globalRule = MiningGlobalRule.Create("AAA", 1m, 1m, 1m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(111, machine.Id, 400, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.SwitchTargetToken(111, "AAA", machineRule.Id, globalRule.Id, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        slot.CompleteSwitching();
        slot.AddTokens(2.75m);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SellMachineAsync(111, slot.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("AAA", data.Symbol);
        Assert.Equal(2, data.TokensCollected);

        var updated = await db.MiningMachineSlots.FindAsync(slot.Id);
        Assert.Equal(0.75m, updated!.TokensAmountCollected);
    }

    [Fact]
    public async Task SellMachineAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slot = await CreateSlotAsync(db, 111);

        var result = await CreateService(db).SellMachineAsync(999, slot.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Трейдера не существует", result.Message);
        Assert.Equal(MiningMachineSlotStatus.Passive, slot.Status);
    }

    [Fact]
    public async Task SellMachineAsync_SlotNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var result = await CreateService(db).SellMachineAsync(111, 999);

        Assert.False(result.IsSuccess);
        Assert.Contains("Слота не существует", result.Message);
    }

    [Fact]
    public async Task SellMachineAsync_NotOwner_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        var owner = await HelpMethods.RegisterTrader(db, 222);
        Assert.True(owner.IsSuccess, owner.Message);

        var slot = await CreateSlotAsync(db, 111, ownerId: 222);

        var result = await CreateService(db).SellMachineAsync(111, slot.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("не владеет", result.Message);
        Assert.Equal(MiningMachineSlotStatus.Passive, slot.Status);
    }

    [Fact]
    public async Task SellMachineAsync_AlreadySold_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);

        var slot = await CreateSlotAsync(db, 111);
        slot.Sell(111, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();

        var result = await CreateService(db).SellMachineAsync(111, slot.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("уже продана", result.Message);
    }
}
