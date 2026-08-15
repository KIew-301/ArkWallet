using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result;

internal class MiningMachineRuleDeletionService(ArkWalletDbContext dbContext, ILogger<MiningMachineRuleDeletionService> logger) : IMiningMachineRuleDeletionService
{
    public async Task<Result> DeleteRuleAsync(long ruleId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachineRulesAsync([ruleId]);

                var rule = await dbContext.MiningMachineRules
                    .FirstOrDefaultAsync(r => r.Id == ruleId);

                if (rule is null)
                    return Fail($"Правило майнинга с Id '{ruleId}' не найдено");

                dbContext.MiningMachineRules.Remove(rule);
                await dbContext.SaveChangesAsync();

                await MiningMachineRecomputeHelper.RecomputeMachinesAsync(dbContext, [rule.MiningMachineId]);

                return Ok();
            });
        }, logger, nameof(MiningMachineRuleDeletionService));
    }
}
