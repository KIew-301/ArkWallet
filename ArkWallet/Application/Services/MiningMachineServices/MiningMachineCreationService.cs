using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<MiningMachineCreationData>;

internal class MiningMachineCreationService(ArkWalletDbContext dbContext, ILogger<MiningMachineCreationService> logger) : IMiningMachineCreationService
{
    public async Task<Result<MiningMachineCreationData>> CreateMachineAsync(MiningMachineCreationCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                if (command == null)
                    return Fail("Команда на создание некорректна");

                var machine = BuildMachine(command);

                var nameExists = await dbContext.MiningMachines.AnyAsync(m => m.Name == machine.Name);
                if (nameExists)
                    return Fail($"Машина с названием '{machine.Name}' уже существует");

                await dbContext.MiningMachines.AddAsync(machine);
                await dbContext.SaveChangesAsync();

                return Ok(new(machine.Id, machine.Name));
            });
        }, logger, nameof(MiningMachineCreationService));
    }

    public async Task<Result<List<MiningMachineCreationData>>> CreateMachinesAsync(IEnumerable<MiningMachineCreationCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var commandList = commands?.ToList();
                if (commandList == null || commandList.Count == 0)
                    return Result<List<MiningMachineCreationData>>.Ok([]);

                var machines = commandList
                    .Select(BuildMachine)
                    .ToList();

                var existingNames = await dbContext.MiningMachines
                    .Where(m => machines.Select(machine => machine.Name).Contains(m.Name))
                    .Select(m => m.Name)
                    .ToListAsync();
                var duplicateInBatch = machines
                    .GroupBy(m => m.Name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToArray();
                var conflicts = existingNames.Concat(duplicateInBatch).Distinct().ToArray();
                if (conflicts.Length > 0)
                    return Result<List<MiningMachineCreationData>>.Fail(
                        $"Машины с названиями уже существуют: {string.Join(", ", conflicts)}");

                await dbContext.MiningMachines.AddRangeAsync(machines);
                await dbContext.SaveChangesAsync();

                return Result<List<MiningMachineCreationData>>.Ok(
                    machines.Select(m => new MiningMachineCreationData(m.Id, m.Name)).ToList());
            });
        }, logger, nameof(MiningMachineCreationService));
    }

    private static MiningMachine BuildMachine(MiningMachineCreationCommand command)
        => MiningMachine.Create(
            MiningMachineTypeParser.Parse(command.Type),
            command.SwitchingTime,
            command.Reusability,
            command.IsActiveForSale,
            command.Image,
            command.Efficiency);
}
