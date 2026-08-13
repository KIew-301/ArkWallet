using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;

internal class MiningGlobalRuleCreationService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MiningGlobalRuleCreationService> logger) : IMiningGlobalRuleCreationService
{
    public async Task<Result> CreateRulesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningGlobalRulesAsync();

                var tokens = await dbContext.CharacterTokens
                    .Where(t => t.IsActive && t.CurrentPrice > 0)
                    .ToListAsync();

                var existingRules = await dbContext.MiningGlobalRules.ToListAsync();
                var rulesByToken = existingRules.ToDictionary(r => r.TokenId);

                foreach (var token in tokens)
                {
                    var baseMiningSpeed = miningEngine.CalculateBaseMiningSpeed(token.CurrentPrice);

                    if (rulesByToken.TryGetValue(token.Symbol, out var rule))
                    {
                        rule.AdvanceCoefficient(miningEngine.NextCoefficient());
                        rule.UpdateBaseMiningSpeed(baseMiningSpeed);
                    }
                    else
                    {
                        var newRule = MiningGlobalRule.Create(
                            token.Symbol,
                            miningEngine.NextCoefficient(),
                            miningEngine.NextCoefficient(),
                            baseMiningSpeed);
                        await dbContext.MiningGlobalRules.AddAsync(newRule);
                    }
                }

                await dbContext.SaveChangesAsync();

                return Result.Ok();
            });
        }, logger, nameof(MiningGlobalRuleCreationService));
    }
}
