using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Mining;

public class MiningMachineDeletionServiceTest
{
    private static async Task<long> CreateMachine(ArkWalletDbContext db, bool isActiveForSale = true)
    {
        var machine = MiningMachine.Create("SM-01", MiningMachineType.SMAI, 10, 50, isActiveForSale, 1000, "img.zzz");
        db.MiningMachines.Add(machine);
        await db.SaveChangesAsync();
        return machine.Id;
    }

    [Fact]
    public async Task DeleteMachineAsync_MachineNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachineAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Contains("не найдена", result.Message);
    }

    [Fact]
    public async Task DeleteMachineAsync_ExistingMachine_DeletesMachineAndRules()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "ZZZ");
        var machineId = await CreateMachine(db);

        var rule = MiningMachineRule.Create(machineId, "ZZZ", 1.5m);
        db.MiningMachineRules.Add(rule);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachineAsync(machineId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.MiningMachines.FindAsync(machineId));
        Assert.Empty(await db.MiningMachineRules.Where(r => r.MiningMachineId == machineId).ToListAsync());
    }

    [Fact]
    public async Task DeleteMachineAsync_MachineWithSlots_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        var machineId = await CreateMachine(db);

        var rule = MiningMachineRule.Create(machineId, "ZZZ", 1.5m);
        db.MiningMachineRules.Add(rule);
        await db.SaveChangesAsync();

        var globalRule = MiningGlobalRule.Create("ZZZ", 1m, 1.2m, 50m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(1001, machineId, 500m, DateTime.UtcNow);
        slot.SwitchTargetToken(1001, "ZZZ", rule.Id, globalRule.Id, 10, DateTime.UtcNow);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachineAsync(machineId);

        Assert.False(result.IsSuccess);
        Assert.Contains("существуют её слоты", result.Message);
        Assert.NotNull(await db.MiningMachines.FindAsync(machineId));
        Assert.NotNull(await db.MiningMachineSlots.FindAsync(slot.Id));
    }

    [Fact]
    public async Task DeactivateMachineAsync_MachineNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeactivateMachineAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Contains("не найдена", result.Message);
    }

    [Fact]
    public async Task DeactivateMachineAsync_AlreadyDeactivated_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machineId = await CreateMachine(db, isActiveForSale: false);
        db.ChangeTracker.Clear();
        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeactivateMachineAsync(machineId);

        Assert.False(result.IsSuccess);
        Assert.Contains("уже деактивирована", result.Message);
    }

    [Fact]
    public async Task DeactivateMachineAsync_ExistingMachine_SetsIsActiveForSaleFalse()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machineId = await CreateMachine(db);
        db.ChangeTracker.Clear();
        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeactivateMachineAsync(machineId);

        Assert.True(result.IsSuccess, result.Message);
        var machine = await db.MiningMachines.FindAsync(machineId);
        Assert.NotNull(machine);
        Assert.False(machine!.IsActiveForSale);
    }
}
