using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.MiningMachineServices;

/// <summary>
/// Помощник пересчёта имени и стоимости машин после изменения правил
/// </summary>
internal static class MiningMachineRecomputeHelper
{
    public static async Task RecomputeMachinesAsync(ArkWalletDbContext dbContext, IEnumerable<long> machineIds)
    {
        var ids = machineIds.Distinct().ToArray();
        if (ids.Length == 0)
            return;

        var machines = await dbContext.MiningMachines
            .Include(m => m.MiningMachineRules)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();

        foreach (var machine in machines)
            machine.RecomputeNameAndCost();

        await dbContext.SaveChangesAsync();
    }
}
