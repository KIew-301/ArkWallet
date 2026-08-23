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
        var machine = MiningMachine.Create(MiningMachineType.SMAI, 10, 50, isActiveForSale, "img.zzz", 1m);
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

        var rule = MiningMachineRule.Create(machineId, "ZZZ", 0.9m);
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
    public async Task DeleteMachineAsync_MachineWithSlots_DeletesMachineKeepsSlots()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ");
        var machineId = await CreateMachine(db);

        var machine = await db.MiningMachines.FindAsync(machineId);

        var globalRule = MiningGlobalRule.Create("ZZZ", 1m, 1.2m, 50m);
        db.MiningGlobalRules.Add(globalRule);
        await db.SaveChangesAsync();

        var slot = MiningMachineSlot.Create(1001, machine!, 500m, DateTime.UtcNow);
        slot.SwitchTargetToken(1001, "ZZZ", globalRule.Id, 10, DateTime.UtcNow);
        db.MiningMachineSlots.Add(slot);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachineAsync(machineId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.MiningMachines.FindAsync(machineId));
        Assert.NotNull(await db.MiningMachineSlots.FindAsync(slot.Id));
        Assert.Equal("ZZZ", (await db.MiningMachineSlots.FindAsync(slot.Id))!.TokenId);
    }

    [Fact]
    public async Task DeleteMachinesAsync_EmptyIds_ReturnsOk()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachinesAsync([]);

        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task DeleteMachinesAsync_MachineNotFound_ReturnsFailAndRollsBack()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var machineId = await CreateMachine(db);
        db.ChangeTracker.Clear();
        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachinesAsync([machineId, 999]);

        Assert.False(result.IsSuccess);
        Assert.Contains("999", result.Message);
        Assert.NotNull(await db.MiningMachines.FindAsync(machineId));
    }

    [Fact]
    public async Task DeleteMachinesAsync_ExistingMachines_DeletesMachinesAndRules()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.CreateToken(db, "ZZZ");
        var firstId = await CreateMachine(db);
        var second = MiningMachine.Create(MiningMachineType.SMAI, 20, 60, true, "img.zzz", 2m);
        db.MiningMachines.Add(second);
        await db.SaveChangesAsync();
        var secondId = second.Id;
        db.MiningMachineRules.Add(MiningMachineRule.Create(firstId, "ZZZ", 0.9m));
        db.MiningMachineRules.Add(MiningMachineRule.Create(secondId, "ZZZ", 0.7m));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MiningMachineDeletionService(db, NullLogger<MiningMachineDeletionService>.Instance);

        var result = await service.DeleteMachinesAsync([firstId, secondId]);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await db.MiningMachines.FindAsync(firstId));
        Assert.Null(await db.MiningMachines.FindAsync(secondId));
        Assert.Empty(await db.MiningMachineRules.ToListAsync());
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
