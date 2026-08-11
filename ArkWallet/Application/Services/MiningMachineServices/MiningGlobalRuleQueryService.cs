using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;

internal class MiningGlobalRuleQueryService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MiningGlobalRuleQueryService> logger) : IMiningGlobalRuleQueryService
{
    public async Task<Result<List<TokensMiningRules>>> TakeRulesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var tokens = await dbContext.CharacterTokens
                .AsNoTracking()
                .Where(t => t.IsActive)
                .ToListAsync();

            if (tokens.Count == 0)
                return Result<List<TokensMiningRules>>.Ok([]);

            var rules = await dbContext.MiningGlobalRules.AsNoTracking().ToListAsync();
            var rulesByToken = rules.ToDictionary(r => r.TokenId);

            var baseProfits = tokens
                .Select(t => miningEngine.CalculateBaseProfit(
                    rulesByToken.GetValueOrDefault(t.Symbol)?.BaseMiningSpeed ?? 0m,
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
                .Select(token =>
                {
                    var rule = rulesByToken.GetValueOrDefault(token.Symbol);
                    var baseMiningSpeed = rule?.BaseMiningSpeed ?? 0m;
                    var baseProfit = miningEngine.CalculateBaseProfit(baseMiningSpeed, token.CurrentPrice);
                    var futureCoefficient = rule?.FutureCoefficient ?? 1m;

                    return new TokensMiningRules(
                        TokenInfoDto.FromEntity(token)!,
                        miningEngine.CalculateStatus(baseProfit, minBaseProfit, maxBaseProfit),
                        miningEngine.CalculateStatus(futureCoefficient, minFuture, maxFuture),
                        baseMiningSpeed,
                        baseProfit);
                })
                .OrderByDescending(r => r.BaseProfit)
                .ToList();

            return Result<List<TokensMiningRules>>.Ok(result);
        }, logger, nameof(MiningGlobalRuleQueryService));
    }
}
