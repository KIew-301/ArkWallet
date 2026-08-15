using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;

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

                var processed = 0;
                foreach (var slot in slots)
                {
                    if (slot.MiningGlobalRule == null)
                        continue;

                    var machineRule = slot.MiningMachineSlotRules
                        .FirstOrDefault(r => r.CharacterTokenId == slot.TokenId);
                    if (machineRule == null)
                        continue;

                    var cash = miningEngine.CalculateCash(
                        slot.MiningGlobalRule.CurrentCoefficient,
                        machineRule.MiningCoefficient,
                        slot.Efficiency,
                        timingCoeff,
                        slot.MiningGlobalRule.BaseTokenMiningSpeed);

                    slot.AddTokens(cash);
                    processed++;
                }

                await dbContext.SaveChangesAsync();

                return Result<int>.Ok(processed);
            });
        }, logger, nameof(MiningMachineSlotCalculationService));
    }
}
