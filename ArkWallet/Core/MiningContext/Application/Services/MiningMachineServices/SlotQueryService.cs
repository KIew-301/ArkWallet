using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.MiningContext.Application.Dtos;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MiningContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;

internal class MiningMachineSlotQueryService(
    ArkWalletDbContext dbContext,
    MiningEngine miningEngine,
    ILogger<MiningMachineSlotQueryService> logger,
    TimeProvider? timeProvider = null) : IMiningMachineSlotQueryService
{
    public async Task<Result<List<MiningMachineSlotData>>> TakeSlotsByTraderAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

            var slots = await dbContext.MiningMachineSlots
                .AsNoTracking()
                .Where(s => s.TraderId == traderId && s.Status != MiningMachineSlotStatus.Sold)
                .Include(s => s.MiningMachineSlotRules)
                .Include(s => s.MiningGlobalRule)
                .Include(s => s.Token)
                .ToListAsync();

            if (slots.Count == 0)
                return Result<List<MiningMachineSlotData>>.Ok([]);

            var tokenIds = slots
                .SelectMany(s => s.MiningMachineSlotRules)
                .Select(r => r.CharacterTokenId)
                .Concat(slots.Where(s => s.TokenId != null).Select(s => s.TokenId!))
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

            var result = slots
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => BuildSlotData(s, tokens, globalRules, now))
                .ToList();

            return Result<List<MiningMachineSlotData>>.Ok(result);
        }, logger, nameof(MiningMachineSlotQueryService));
    }

    private MiningMachineSlotData BuildSlotData(
        MiningMachineSlot slot,
        Dictionary<string, CharacterToken> tokens,
        Dictionary<string, MiningGlobalRule> globalRules,
        DateTime now)
        => MiningContextMapper.BuildSlotData(miningEngine, slot, tokens, globalRules, now);
}
