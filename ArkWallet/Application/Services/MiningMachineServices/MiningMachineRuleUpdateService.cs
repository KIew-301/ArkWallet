using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result;

internal class MiningMachineRuleUpdateService(ArkWalletDbContext dbContext, ILogger<MiningMachineRuleUpdateService> logger) : IMiningMachineRuleUpdateService
{
    public async Task<Result> UpdateRuleAsync(MiningMachineRuleUpdateCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (command == null)
                return Fail("Команда на изменение правила некорректна");

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachineRulesAsync([command.MiningRuleId]);

                var rule = await dbContext.MiningMachineRules
                    .FirstOrDefaultAsync(r => r.Id == command.MiningRuleId);

                if (rule is null)
                    return Fail($"Правило майнинга с Id '{command.MiningRuleId}' не найдено");

                rule.UpdateCoefficient(command.MiningCoefficient);
                await MiningMachineRecomputeHelper.RecomputeMachinesAsync(dbContext, [rule.MiningMachineId]);
                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(MiningMachineRuleUpdateService));
    }
}
