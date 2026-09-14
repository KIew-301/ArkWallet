using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MiningContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;

internal class MiningMachineSlotCalculationService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MiningMachineSlotCalculationService> logger) : IMiningMachineSlotCalculationService
{
    public async Task<Result<int>> TakeTokensOnMachinesAsync(decimal timingCoeff = 1)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (timingCoeff <= 0)
                return Result<int>.Fail("Коэффициент времени должен быть больше нуля");

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockActiveMiningMachineSlotsAsync();

                var slots = await dbContext.MiningMachineSlots
                    .Include(s => s.MiningMachineSlotRules)
                    .Include(s => s.MiningGlobalRule)
                    .Where(s => s.Status == MiningMachineSlotStatus.Active)
                    .ToListAsync();

                var processed = ProcessSlots(slots, timingCoeff);

                await dbContext.SaveChangesAsync();

                return Result<int>.Ok(processed);
            });
        }, logger, nameof(MiningMachineSlotCalculationService));
    }

    private int ProcessSlots(List<MiningMachineSlot> slots, decimal timingCoeff)
    {
        return slots.Count(slot => MiningContextMapper.AccumulateTokens(miningEngine, slot, timingCoeff));
    }
}
