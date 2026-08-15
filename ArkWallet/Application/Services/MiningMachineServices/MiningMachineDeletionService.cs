using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result;

internal class MiningMachineDeletionService(ArkWalletDbContext dbContext, ILogger<MiningMachineDeletionService> logger) : IMiningMachineDeletionService
{
    public async Task<Result> DeleteMachineAsync(long machineId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachinesAsync([machineId]);

                var machine = await dbContext.MiningMachines
                    .FirstOrDefaultAsync(m => m.Id == machineId);

                if (machine is null)
                    return Fail($"Майнинг-машина с Id '{machineId}' не найдена");

                await dbContext.MiningMachineRules
                    .Where(r => r.MiningMachineId == machineId)
                    .ExecuteDeleteAsync();

                dbContext.MiningMachines.Remove(machine);
                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(MiningMachineDeletionService));
    }

    public async Task<Result> DeactivateMachineAsync(long machineId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachinesAsync([machineId]);

                var machine = await dbContext.MiningMachines
                    .FirstOrDefaultAsync(m => m.Id == machineId);

                if (machine is null)
                    return Result.Fail($"Майнинг-машина с Id '{machineId}' не найдена");

                if (!machine.IsActiveForSale)
                    return Result.Fail($"Майнинг-машина с Id '{machineId}' уже деактивирована");

                machine.SetActiveForSale(false);
                await dbContext.SaveChangesAsync();

                return Result.Ok();
            });
        }, logger, nameof(MiningMachineDeletionService));
    }
}
