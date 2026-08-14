using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;

internal class MiningMachineQueryService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MiningMachineQueryService> logger) : IMiningMachineQueryService
{
    public async Task<Result<List<MiningMachineData>>> TakeActiveForSaleMachinesAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ownedMachineIds = await dbContext.MiningMachineSlots
                .AsNoTracking()
                .Where(s => s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold)
                .Select(s => s.MiningMachineId)
                .Distinct()
                .ToArrayAsync();

            var machines = await dbContext.MiningMachines
                .AsNoTracking()
                .Where(m => m.IsActiveForSale && !ownedMachineIds.Contains(m.Id))
                .Include(m => m.MiningMachineRules)
                .ToListAsync();

            if (machines.Count == 0)
                return Result<List<MiningMachineData>>.Ok([]);

            var tokenIds = machines
                .SelectMany(m => m.MiningMachineRules)
                .Select(r => r.CharacterTokenId)
                .Distinct()
                .ToArray();

            var tokens = await dbContext.CharacterTokens
                .AsNoTracking()
                .Where(t => tokenIds.Contains(t.Symbol))
                .ToDictionaryAsync(t => t.Symbol);

            var globalRules = await dbContext.MiningGlobalRules
                .AsNoTracking()
                .Where(r => tokenIds.Contains(r.TokenId))
                .ToDictionaryAsync(r => r.TokenId);

            var result = machines
                .Select(m => BuildMachineData(m, tokens, globalRules))
                .OrderBy(d => d.Cost)
                .ToList();

            return Result<List<MiningMachineData>>.Ok(result);
        }, logger, nameof(MiningMachineQueryService));
    }

    private MiningMachineData BuildMachineData(
        MiningMachine machine,
        Dictionary<string, CharacterToken> tokens,
        Dictionary<string, MiningGlobalRule> globalRules)
    {
        var effective = new List<TokensMiningData>();
        var stable = new List<TokensMiningData>();
        foreach (var rule in machine.MiningMachineRules)
        {
            if (!tokens.TryGetValue(rule.CharacterTokenId, out var token))
                continue;

            var globalRule = globalRules.GetValueOrDefault(token.Symbol);
            var miningSpeed = miningEngine.CalculateMiningSpeed(
                globalRule?.CurrentCoefficient ?? 1m,
                rule.MiningCoefficient,
                globalRule?.BaseTokenMiningSpeed ?? 0m);
            var profit = miningEngine.CalculateProfit(miningSpeed, token.CurrentPrice);
            var tokenData = new TokensMiningData(token.IconUrl, token.Symbol, miningSpeed, profit);

            if (rule.MiningCoefficient >= MiningEngine.EffectiveMiningCoefficientMin
                && rule.MiningCoefficient <= MiningEngine.EffectiveMiningCoefficientMax)
            {
                effective.Add(tokenData);
            }
            else if (rule.MiningCoefficient >= MiningEngine.StableMiningCoefficientMin
                && rule.MiningCoefficient < MiningEngine.StableMiningCoefficientMax)
            {
                stable.Add(tokenData);
            }
        }

        var effectiveSorted = effective.OrderByDescending(d => d.Profit).ToList();
        var stableSorted = stable.OrderByDescending(d => d.Profit).ToList();
        var maxProfit = effectiveSorted.Concat(stableSorted).Select(d => d.Profit).DefaultIfEmpty(0m).Max();

        return new MiningMachineData(
            machine.Id,
            machine.Name,
            machine.Type.ToString(),
            maxProfit,
            machine.SwitchingTime,
            machine.Reusability,
            machine.Cost,
            effectiveSorted,
            stableSorted);
    }
}
