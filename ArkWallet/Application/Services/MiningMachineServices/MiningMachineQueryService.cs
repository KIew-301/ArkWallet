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
    public async Task<Result<List<MiningMachineData>>> TakeActiveForSaleMachinesAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var machines = await dbContext.MiningMachines
                .AsNoTracking()
                .Where(m => m.IsActiveForSale)
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
        var tokensMiningData = new List<TokensMiningData>();
        foreach (var rule in machine.MiningMachineRules)
        {
            if (!tokens.TryGetValue(rule.CharacterTokenId, out var token))
                continue;

            var globalRule = globalRules.GetValueOrDefault(token.Symbol);
            var miningSpeed = miningEngine.CalculateMiningSpeed(
                globalRule?.CurrentCoefficient ?? 1m,
                rule.MiningCoefficient,
                globalRule?.BaseMiningSpeed ?? 0m);
            var profit = miningEngine.CalculateProfit(miningSpeed, token.CurrentPrice);

            tokensMiningData.Add(new(token.IconUrl, token.Symbol, miningSpeed, profit));
        }

        return new MiningMachineData(
            machine.Id,
            machine.Name,
            machine.Type.ToString(),
            tokensMiningData.Count > 0 ? tokensMiningData.Max(d => d.Profit) : 0m,
            machine.SwitchingTime,
            machine.Reusability,
            machine.Cost,
            tokensMiningData.OrderByDescending(d => d.Profit).ToList());
    }
}
