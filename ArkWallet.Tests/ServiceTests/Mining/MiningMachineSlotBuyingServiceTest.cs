using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineSlotBuyingServiceTest
{
    private static MiningMachineSlotBuyingService CreateService(ArkWalletDbContext db) =>
        new(db, NullLogger<MiningMachineSlotBuyingService>.Instance);

    private static MiningMachine CreateMachine(
        ArkWalletDbContext db,
        string name = "SM-01",
        decimal cost = 1000,
        decimal reusability = 80,
        bool isActiveForSale = true)
    {
        var machine = MiningMachine.Create(
            name, MiningMachineType.SMAI, 10, reusability, isActiveForSale, cost, "img.zzz");
        db.MiningMachines.Add(machine);
        return machine;
    }

    [Fact]
    public async Task BuyMachineAsync_ValidPurchase_ChargesFullCostAndSavesResalePrice()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 1000);

        var machine = CreateMachine(db, cost: 550, reusability: 80);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(111, machine.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var slotId));

        var traderEntity = await HelpMethods.GetTrader(db, 111);
        Assert.Equal(1450m, traderEntity!.Balance);

        var slot = await db.MiningMachineSlots.FindAsync(slotId);
        Assert.NotNull(slot);
        Assert.Equal(machine.Id, slot!.MiningMachineId);
        Assert.Equal(440m, slot.Cost);
        Assert.Equal(MiningMachineSlotStatus.Passive, slot.Status);
    }

    [Fact]
    public async Task BuyMachineAsync_InsufficientFunds_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 300);

        var machine = CreateMachine(db, cost: 2000);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(111, machine.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Недостаточно средств", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_MoreThanTenMachines_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 1000000);

        var machine = CreateMachine(db, cost: 100);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        for (var i = 0; i < 10; i++)
        {
            var result = await service.BuyMachineAsync(111, machine.Id);
            Assert.True(result.IsSuccess, $"Purchase #{i}: {result.Message}");
        }

        var eleventh = await service.BuyMachineAsync(111, machine.Id);

        Assert.False(eleventh.IsSuccess);
        Assert.Contains("10", eleventh.Message);
        Assert.Equal(10, await db.MiningMachineSlots.CountAsync(s => s.TraderId == 111));
    }

    [Fact]
    public async Task BuyMachineAsync_TenMachinesButOneSold_AllowsPurchase()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 1000000);

        var machine = CreateMachine(db, cost: 100);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        for (var i = 0; i < 10; i++)
        {
            var result = await service.BuyMachineAsync(111, machine.Id);
            Assert.True(result.IsSuccess, result.Message);
        }

        var soldSlot = await db.MiningMachineSlots.FirstAsync(s => s.TraderId == 111);
        soldSlot.Sell(111, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();

        var afterSell = await service.BuyMachineAsync(111, machine.Id);

        Assert.True(afterSell.IsSuccess, afterSell.Message);
        Assert.Equal(10, await db.MiningMachineSlots.CountAsync(s => s.TraderId == 111 && s.Status != MiningMachineSlotStatus.Sold));
    }

    [Fact]
    public async Task BuyMachineAsync_MachineNotForSale_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 10000);

        var machine = CreateMachine(db, isActiveForSale: false);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(111, machine.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("недоступна", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_MachineNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 10000);

        var result = await CreateService(db).BuyMachineAsync(111, 999);

        Assert.False(result.IsSuccess);
        Assert.Contains("не существует", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machine = CreateMachine(db);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BuyMachineAsync(999, machine.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("Трейдера не существует", result.Message);
    }

    [Fact]
    public async Task BuyMachineAsync_BalanceNotChangedOnFailure()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var trader = await HelpMethods.RegisterTrader(db, 111);
        Assert.True(trader.IsSuccess, trader.Message);
        await HelpMethods.GiveMoney(db, 111, 300);

        var machine = CreateMachine(db, cost: 2000);
        await db.SaveChangesAsync();

        await CreateService(db).BuyMachineAsync(111, machine.Id);

        var traderEntity = await HelpMethods.GetTrader(db, 111);
        Assert.Equal(1300m, traderEntity!.Balance);
        Assert.Empty(await db.MiningMachineSlots.ToListAsync());
    }
}
