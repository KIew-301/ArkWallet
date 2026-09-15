using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.MiningContext.Application.Dtos;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MiningContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;

internal class MiningGlobalRuleQueryService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MiningGlobalRuleQueryService> logger) : IMiningGlobalRuleQueryService
{
    public async Task<Result<List<TokensMiningRuleData>>> TakeRulesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await ComputeRulesAsync();
        }, logger, nameof(MiningGlobalRuleQueryService));
    }

    private async Task<Result<List<TokensMiningRuleData>>> ComputeRulesAsync()
    {
        var tokens = await dbContext.CharacterTokens
            .AsNoTracking()
            .Where(t => t.IsActive)
            .ToListAsync();

        if (tokens.Count == 0)
            return Result<List<TokensMiningRuleData>>.Ok([]);

        var rules = await dbContext.MiningGlobalRules.AsNoTracking().ToListAsync();
        var rulesByToken = rules.ToDictionary(r => r.TokenId);

        var baseProfits = tokens
            .Select(t => miningEngine.CalculateBaseProfit(
                rulesByToken.GetValueOrDefault(t.Symbol)?.BaseTokenMiningSpeed ?? 0m,
                t.CurrentPrice))
            .ToArray();

        var futureCoefficients = tokens
            .Select(t => rulesByToken.GetValueOrDefault(t.Symbol)?.FutureCoefficient ?? 1m)
            .ToArray();

        var minBaseProfit = baseProfits.Min();
        var maxBaseProfit = baseProfits.Max();
        var minFuture = futureCoefficients.Min();
        var maxFuture = futureCoefficients.Max();

        var result = tokens
            .Select(token => MiningContextMapper.BuildRuleData(
                miningEngine,
                token,
                rulesByToken.GetValueOrDefault(token.Symbol),
                minBaseProfit,
                maxBaseProfit,
                minFuture,
                maxFuture))
            .OrderByDescending(r => r.BaseProfit)
            .ToList();

        return Result<List<TokensMiningRuleData>>.Ok(result);
    }
}
