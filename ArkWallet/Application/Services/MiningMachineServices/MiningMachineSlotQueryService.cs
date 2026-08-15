using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;

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
    {
        var switchingPercent = miningEngine.CalculateSwitchingPercent(
            now, slot.StartSwitchingDateTime, slot.EndSwitchingDateTime);

        var activeToken = ActiveTokenMiningData.Empty();
        if (slot.TokenId != null && tokens.TryGetValue(slot.TokenId, out var activeTokenEntity))
        {
            var machineRule = slot.MiningMachineSlotRules
                .FirstOrDefault(r => r.CharacterTokenId == slot.TokenId);
            var globalRule = slot.MiningGlobalRule
                ?? globalRules.GetValueOrDefault(slot.TokenId);

            var miningSpeed = miningEngine.CalculateMiningSpeed(
                globalRule?.CurrentCoefficient ?? 1m,
                machineRule?.MiningCoefficient ?? 0m,
                globalRule?.BaseTokenMiningSpeed ?? 0m);
            var profit = miningEngine.CalculateProfit(miningSpeed, activeTokenEntity.CurrentPrice);

            activeToken = new ActiveTokenMiningData(
                activeTokenEntity.IconUrl,
                activeTokenEntity.Symbol,
                miningSpeed,
                profit);
        }

        var effective = new List<TokensMiningData>();
        var stable = new List<TokensMiningData>();
        foreach (var rule in slot.MiningMachineSlotRules)
        {
            if (rule.CharacterTokenId == slot.TokenId)
                continue;
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

        return new MiningMachineSlotData(
            slot.Id,
            slot.Name,
            slot.Type.ToString(),
            slot.Status.ToString(),
            slot.TokensAmountCollected,
            switchingPercent,
            slot.SwitchingTime,
            slot.Cost,
            activeToken,
            effective.OrderByDescending(d => d.Profit).ToList(),
            stable.OrderByDescending(d => d.Profit).ToList());
    }
}
