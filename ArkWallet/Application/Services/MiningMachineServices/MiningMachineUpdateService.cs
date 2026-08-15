using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result;

internal class MiningMachineUpdateService(ArkWalletDbContext dbContext, ILogger<MiningMachineUpdateService> logger) : IMiningMachineUpdateService
{
    public async Task<Result> UpdateMachineAsync(MiningMachineUpdateCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (command == null)
                return Fail("Команда на изменение машины некорректна");

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachinesAsync([command.MachineId]);

                var machine = await dbContext.MiningMachines
                    .Include(m => m.MiningMachineRules)
                    .FirstOrDefaultAsync(m => m.Id == command.MachineId);

                if (machine is null)
                    return Fail($"Майнинг-машина с Id '{command.MachineId}' не найдена");

                machine.Update(
                    command.Type != null ? MiningMachineTypeParser.Parse(command.Type) : null,
                    command.SwitchingTime,
                    command.Reusability,
                    command.IsActiveForSale,
                    command.Image,
                    command.Efficiency);

                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(MiningMachineUpdateService));
    }
}
