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

            return Ok(rule.Id);
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

            var existingMachines = await dbContext.MiningMachines
                .Where(m => machineIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();
            if (existingMachines.Count != machineIds.Length)
                return Result<List<long>>.Fail("Одна из машин не существует");

            var existingTokens = await dbContext.CharacterTokens
                .Where(t => symbols.Contains(t.Symbol))
                .Select(t => t.Symbol)
                .ToListAsync();
            if (existingTokens.Count != symbols.Length)
                return Result<List<long>>.Fail("Один из токенов не существует");

            var existingRules = await dbContext.MiningMachineRules
                .Where(r => machineIds.Contains(r.MiningMachineId))
                .ToListAsync();

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
                    return Result<List<long>>.Fail("Правило для такой связки машины и токена уже существует");

                var totalCount = existingCountByMachine.GetValueOrDefault(command.MiningMachineId) +
                                 incomingCountByMachine.GetValueOrDefault(command.MiningMachineId);
                if (totalCount > MaxRulesPerMachine)
                    return Result<List<long>>.Fail($"Нельзя добавить больше {MaxRulesPerMachine} правил для одной машины");
            }

            var rules = commandList
                .Select(c => MiningMachineRule.Create(c.MiningMachineId, c.CharacterTokenId, c.MiningCoefficient))
                .ToList();

            await dbContext.MiningMachineRules.AddRangeAsync(rules);
            await dbContext.SaveChangesAsync();

            return Result<List<long>>.Ok(rules.Select(r => r.Id).ToList());
        }, logger, nameof(MiningMachineRuleCreationService));
    }
}
