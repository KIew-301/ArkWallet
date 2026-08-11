using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Orchestrators;

internal class MiningMachineCreationOrchestrator(
    ArkWalletDbContext dbContext,
    IMiningMachineCreationService machineCreationService,
    IMiningMachineRuleCreationService ruleCreationService,
    ILogger<MiningMachineCreationOrchestrator> logger) : IMiningMachineCreationOrchestrator
{
    public async Task<Result<MiningMachineCreationData>> CreateMachineAsync(MiningMachineCreationCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var machineResult = await machineCreationService.CreateMachineAsync(command);
                if (!machineResult.IsSuccess)
                    return Result<MiningMachineCreationData>.Fail(machineResult.Message);
                if (!machineResult.TryGetData(out var machineData))
                    return Result<MiningMachineCreationData>.Fail("Не удалось создать машину");

                if (command?.Rules is { Count: > 0 } rules)
                {
                    var rulesCommands = rules
                        .Select(r => new MiningMachineRuleCreationCommand(machineData.Id, r.CharacterTokenId, r.MiningCoefficient))
                        .ToList();

                    var rulesResult = await ruleCreationService.CreateRulesAsync(rulesCommands);
                    if (!rulesResult.IsSuccess)
                        return Result<MiningMachineCreationData>.Fail(rulesResult.Message);
                }

                return Result<MiningMachineCreationData>.Ok(machineData);
            });
        }, logger, nameof(MiningMachineCreationOrchestrator));
    }

    public async Task<Result<List<MiningMachineCreationData>>> CreateMachinesAsync(IEnumerable<MiningMachineCreationCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var commandList = commands?.ToList() ?? [];
                if (commandList.Count == 0)
                    return Result<List<MiningMachineCreationData>>.Ok([]);

                var machineResult = await machineCreationService.CreateMachinesAsync(commandList);
                if (!machineResult.IsSuccess)
                    return Result<List<MiningMachineCreationData>>.Fail(machineResult.Message);
                if (!machineResult.TryGetData(out var machines))
                    return Result<List<MiningMachineCreationData>>.Fail("Не удалось создать машины");

                var rulesCommands = new List<MiningMachineRuleCreationCommand>();
                for (var i = 0; i < machines.Count; i++)
                {
                    if (commandList[i].Rules is not { Count: > 0 } rules)
                        continue;

                    rulesCommands.AddRange(rules
                        .Select(r => new MiningMachineRuleCreationCommand(machines[i].Id, r.CharacterTokenId, r.MiningCoefficient)));
                }

                if (rulesCommands.Count > 0)
                {
                    var rulesResult = await ruleCreationService.CreateRulesAsync(rulesCommands);
                    if (!rulesResult.IsSuccess)
                        return Result<List<MiningMachineCreationData>>.Fail(rulesResult.Message);
                }

                return Result<List<MiningMachineCreationData>>.Ok(machines);
            });
        }, logger, nameof(MiningMachineCreationOrchestrator));
    }
}
