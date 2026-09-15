using ArkWallet.Core.MiningContext.Application.Dtos;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.TradingContext.Application.Dtos;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;

/// <summary>
/// Maps between mining persistence records, the MiningEngine and read models.
/// Services load records, delegate mapping here, and save.
/// </summary>
internal static class MiningContextMapper
{
    internal static MiningGlobalRule CreateRule(
        string tokenSymbol,
        decimal currentCoefficient,
        decimal futureCoefficient,
        decimal baseTokenMiningSpeed)
        => MiningGlobalRule.Create(tokenSymbol, currentCoefficient, futureCoefficient, baseTokenMiningSpeed);

    internal static void AdvanceRule(MiningGlobalRule rule, decimal newFutureCoefficient, decimal baseTokenMiningSpeed)
    {
        rule.Update(rule.FutureCoefficient, newFutureCoefficient, baseTokenMiningSpeed);
    }

    internal static TokensMiningRuleData BuildRuleData(
        MiningEngine engine,
        CharacterToken token,
        MiningGlobalRule? rule,
        decimal minBaseProfit,
        decimal maxBaseProfit,
        decimal minFuture,
        decimal maxFuture)
    {
        var baseTokenMiningSpeed = rule?.BaseTokenMiningSpeed ?? 0m;
        var baseProfit = engine.CalculateBaseProfit(baseTokenMiningSpeed, token.CurrentPrice);
        var futureCoefficient = rule?.FutureCoefficient ?? 1m;

        return new TokensMiningRuleData(
            TokenInfoDto.FromEntity(token)!,
            engine.CalculateStatus(baseProfit, minBaseProfit, maxBaseProfit).ToString(),
            engine.CalculateStatus(futureCoefficient, minFuture, maxFuture).ToString(),
            baseTokenMiningSpeed,
            baseProfit);
    }

    internal static MiningMachineData BuildMachineData(
        MiningEngine engine,
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
            var miningSpeed = engine.CalculateMiningSpeed(
                globalRule?.CurrentCoefficient ?? 0m,
                rule.MiningCoefficient,
                machine.Efficiency,
                globalRule?.BaseTokenMiningSpeed ?? 0m);
            var profit = engine.CalculateProfit(miningSpeed, token.CurrentPrice);
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

    internal static MiningMachineSlotData BuildSlotData(
        MiningEngine engine,
        MiningMachineSlot slot,
        Dictionary<string, CharacterToken> tokens,
        Dictionary<string, MiningGlobalRule> globalRules,
        DateTime now)
    {
        var switchingPercent = engine.CalculateSwitchingPercent(
            now, slot.StartSwitchingDateTime, slot.EndSwitchingDateTime);

        var activeToken = ActiveTokenMiningData.Empty();
        if (slot.TokenId != null && tokens.TryGetValue(slot.TokenId, out var activeTokenEntity))
        {
            var machineRule = slot.MiningMachineSlotRules
                .FirstOrDefault(r => r.CharacterTokenId == slot.TokenId);
            var globalRule = slot.MiningGlobalRule
                ?? globalRules.GetValueOrDefault(slot.TokenId);

            var miningSpeed = engine.CalculateMiningSpeed(
                globalRule?.CurrentCoefficient ?? 0m,
                machineRule?.MiningCoefficient ?? 0m,
                slot.Efficiency,
                globalRule?.BaseTokenMiningSpeed ?? 0m);
            var profit = engine.CalculateProfit(miningSpeed, activeTokenEntity.CurrentPrice);

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
            var miningSpeed = engine.CalculateMiningSpeed(
                globalRule?.CurrentCoefficient ?? 0m,
                rule.MiningCoefficient,
                slot.Efficiency,
                globalRule?.BaseTokenMiningSpeed ?? 0m);
            var profit = engine.CalculateProfit(miningSpeed, token.CurrentPrice);
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

    /// <summary>Восстанавливает доменную машину слота из сущности для переходов бизнес-логики.</summary>
    internal static Machine MachineFrom(MiningMachineSlot slot)
    {
        return Machine.Load(new MachineLoadCommand(
            slot.TraderId,
            (ArkWallet.Core.ShoppingContext.Domain.Machine.MachineType)slot.Type,
            slot.SwitchingTime,
            slot.Efficiency,
            slot.Image,
            slot.Cost,
            slot.CreatedAt,
            slot.TokenId,
            slot.MiningGlobalRuleId,
            Map(slot.Status),
            slot.StartSwitchingDateTime,
            slot.EndSwitchingDateTime,
            slot.TokensAmountCollected,
            slot.SoldAt));
    }

    private static MachineStatus Map(MiningMachineSlotStatus status)
    {
        return status switch
        {
            MiningMachineSlotStatus.Active => MachineStatus.Active,
            MiningMachineSlotStatus.Passive => MachineStatus.Passive,
            MiningMachineSlotStatus.Switching => MachineStatus.Switching,
            MiningMachineSlotStatus.Sold => MachineStatus.Sold,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    internal static bool AccumulateTokens(MiningEngine engine, MiningMachineSlot slot, decimal timingCoeff)
    {
        if (slot.MiningGlobalRule == null)
            return false;

        var machineRule = slot.MiningMachineSlotRules
            .FirstOrDefault(r => r.CharacterTokenId == slot.TokenId);
        if (machineRule == null)
            return false;

        var cash = engine.CalculateCash(
            slot.MiningGlobalRule.CurrentCoefficient,
            machineRule.MiningCoefficient,
            slot.Efficiency,
            timingCoeff,
            slot.MiningGlobalRule.BaseTokenMiningSpeed);

        var machine = MachineFrom(slot);
        machine.AddTokens(cash);
        slot.Update(machine);
        return true;
    }
}