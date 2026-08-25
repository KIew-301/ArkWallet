using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<long>;

internal class MiningMachineRuleCreationService(ArkWalletDbContext dbContext, ILogger<MiningMachineRuleCreationService> logger) : IMiningMachineRuleCreationService
{
    private const int MaxRulesPerMachine = 10;

    public async Task<Result<long>> CreateRuleAsync(MiningMachineRuleCreationCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (command == null)
                return Fail("Команда на создание правила некорректна");

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachinesAsync([command.MiningMachineId]);

                var machineExists = await dbContext.MiningMachines.AnyAsync(m => m.Id == command.MiningMachineId);
                if (!machineExists)
                    return Fail("Машины не существует");

                var tokenExists = await dbContext.CharacterTokens.AnyAsync(t => t.Symbol == command.CharacterTokenId);
                if (!tokenExists)
                    return Fail("Токена не существует");

                var ruleExists = await dbContext.MiningMachineRules.AnyAsync(r =>
                    r.MiningMachineId == command.MiningMachineId &&
                    r.CharacterTokenId == command.CharacterTokenId);
                if (ruleExists)
                    return Fail("Правило для такой связки машины и токена уже существует");

                var rulesCount = await dbContext.MiningMachineRules.CountAsync(r => r.MiningMachineId == command.MiningMachineId);
                if (rulesCount >= MaxRulesPerMachine)
                    return Fail($"Нельзя добавить больше {MaxRulesPerMachine} правил для одной машины");

                var rule = MiningMachineRule.Create(command.MiningMachineId, command.CharacterTokenId, command.MiningCoefficient);
                await dbContext.MiningMachineRules.AddAsync(rule);
                await dbContext.SaveChangesAsync();

                await MiningMachineRecomputeHelper.RecomputeMachinesAsync(dbContext, [command.MiningMachineId]);

                return Ok(rule.Id);
            });
        }, logger, nameof(MiningMachineRuleCreationService));
    }

    public async Task<Result<List<long>>> CreateRulesAsync(IEnumerable<MiningMachineRuleCreationCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var commandList = commands?.ToList();
            if (commandList == null || commandList.Count == 0)
                return Result<List<long>>.Ok([]);

            var machineIds = commandList.Select(c => c.MiningMachineId).Distinct().ToArray();
            var symbols = commandList.Select(c => c.CharacterTokenId).Distinct().ToArray();

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningMachinesAsync(machineIds);

                var validationError = await ValidateRulesCreationAsync(commandList, machineIds, symbols);
                if (validationError != null)
                    return Result<List<long>>.Fail(validationError);

                var rules = commandList
                    .Select(c => MiningMachineRule.Create(c.MiningMachineId, c.CharacterTokenId, c.MiningCoefficient))
                    .ToList();

                await dbContext.MiningMachineRules.AddRangeAsync(rules);
                await dbContext.SaveChangesAsync();

                await MiningMachineRecomputeHelper.RecomputeMachinesAsync(dbContext, machineIds);

                return Result<List<long>>.Ok(rules.Select(r => r.Id).ToList());
            });
        }, logger, nameof(MiningMachineRuleCreationService));
    }

    /// <summary>Verifies machines and tokens exist and that no duplicate rules or per-machine limits are violated.</summary>
    private async Task<string?> ValidateRulesCreationAsync(
        List<MiningMachineRuleCreationCommand> commandList,
        long[] machineIds,
        string[] symbols)
    {
        if (!await MachinesExistAsync(machineIds))
            return "Одна из машин не существует";

        if (!await TokensExistAsync(symbols))
            return "Один из токенов не существует";

        var existingRules = await dbContext.MiningMachineRules
            .Where(r => machineIds.Contains(r.MiningMachineId))
            .ToListAsync();

        return ValidateNoDuplicateOrExcessiveRules(commandList, existingRules);
    }

    /// <summary>Determines whether all specified machines exist.</summary>
    private async Task<bool> MachinesExistAsync(long[] machineIds)
    {
        var existingMachines = await dbContext.MiningMachines
            .Where(m => machineIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync();
        return existingMachines.Count == machineIds.Length;
    }

    /// <summary>Determines whether all specified tokens exist.</summary>
    private async Task<bool> TokensExistAsync(string[] symbols)
    {
        var existingTokens = await dbContext.CharacterTokens
            .Where(t => symbols.Contains(t.Symbol))
            .Select(t => t.Symbol)
            .ToListAsync();
        return existingTokens.Count == symbols.Length;
    }

    /// <summary>Checks that no machine-token pair is duplicated and no machine exceeds the maximum number of rules.</summary>
    private static string? ValidateNoDuplicateOrExcessiveRules(
        List<MiningMachineRuleCreationCommand> commandList,
        List<MiningMachineRule> existingRules)
    {
        var existingPairs = existingRules
            .Select(r => (r.MiningMachineId, r.CharacterTokenId))
            .ToHashSet();

        var existingCountByMachine = existingRules
            .GroupBy(r => r.MiningMachineId)
            .ToDictionary(g => g.Key, g => g.Count());

        var incomingCountByMachine = commandList
            .GroupBy(c => c.MiningMachineId)
            .ToDictionary(g => g.Key, g => g.Count());

        var addedPairs = new HashSet<(long, string)>();
        foreach (var command in commandList)
        {
            var pair = (command.MiningMachineId, command.CharacterTokenId);
            if (existingPairs.Contains(pair) || !addedPairs.Add(pair))
                return "Правило для такой связки машины и токена уже существует";

            var totalCount = existingCountByMachine.GetValueOrDefault(command.MiningMachineId) +
                             incomingCountByMachine.GetValueOrDefault(command.MiningMachineId);
            if (totalCount > MaxRulesPerMachine)
                return $"Нельзя добавить больше {MaxRulesPerMachine} правил для одной машины";
        }

        return null;
    }
}
