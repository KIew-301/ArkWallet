using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.MiningContext.Application.Dtos;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices;

internal class MachineQueryService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MachineQueryService> logger) : IMachineQueryService
{
    public async Task<Result<List<MiningMachineData>>> TakeActiveForSaleMachinesAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var ownedMachineNames = await dbContext.MiningMachineSlots
                .AsNoTracking()
                .Where(s => s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold)
                .Select(s => s.Name)
                .Distinct()
                .ToArrayAsync();

            var machines = await dbContext.MiningMachines
                .AsNoTracking()
                .Where(m => m.IsActiveForSale && !ownedMachineNames.Contains(m.Name))
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
        }, logger, nameof(MachineQueryService));
    }

    private MiningMachineData BuildMachineData(
        MiningMachine machine,
        Dictionary<string, CharacterToken> tokens,
        Dictionary<string, MiningGlobalRule> globalRules)
        => MiningContextMapper.BuildMachineData(miningEngine, machine, tokens, globalRules);
}
