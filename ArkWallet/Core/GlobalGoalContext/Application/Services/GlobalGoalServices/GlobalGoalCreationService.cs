using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.GlobalGoalServices;
using ArkWallet.Domain.GlobalGoalContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

/// <summary>
/// Создание глобальных целей и добавление промежуточных шагов.
/// </summary>
internal class GlobalGoalCreationService(
    ArkWalletDbContext dbContext,
    IEnumerable<IDomainGlobalGoalCalculation> calculations,
    ILogger<GlobalGoalCreationService> logger) : IGlobalGoalCreationService
{
    public async Task<Result<long>> CreateGoalAsync(CreateGlobalGoalCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var goal = GlobalGoal.CreateNew(
                0, command.Name, command.Description, command.Target,
                command.SymbolForReward, command.AmountForReward);

            var record = Records.GlobalGoal.Create(
                goal.Id,
                goal.Name,
                goal.Description,
                goal.Target,
                goal.Actual,
                goal.Progress,
                goal.AchievedCount);

            dbContext.GlobalGoals.Add(record);
            await dbContext.SaveChangesAsync();

            var firstStep = goal.Steps.First(s => s.StepNumber == 1);
            dbContext.GlobalGoalSteps.Add(Records.GlobalGoalStep.Create(
                record.Id,
                firstStep.StepNumber,
                firstStep.Target,
                firstStep.SymbolForReward,
                firstStep.AmountForReward));

            await ApplyInitialActualAsync(record);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Global goal created: {Name} (Id {Id}, target {Target}, actual {Actual})",
                record.Name, record.Id, record.Target, record.Actual);

            return Result<long>.Ok(record.Id);
        }, logger, nameof(GlobalGoalCreationService));
    }

    private async Task ApplyInitialActualAsync(Records.GlobalGoal record)
    {
        var calculation = calculations.FirstOrDefault(c => c.GoalName == record.Name);
        if (calculation is null)
            return;

        var actual = await calculation.CalculateAsync(dbContext);

        record.Actual = actual;
        record.Progress = ComputeProgress(record.Target, actual);
    }

    private static decimal ComputeProgress(decimal target, decimal actual)
    {
        if (target <= 0)
            return 0;

        var progress = actual / target;
        return progress < 0 ? 0 : progress;
    }

    public async Task<Result> AddStepAsync(AddGlobalGoalStepCommand command)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var record = await dbContext.GlobalGoals
                .Include(g => g.Steps)
                .FirstOrDefaultAsync(g => g.Id == command.GoalId);

            if (record is null)
                return Result.Fail($"Цель с ID {command.GoalId} не найдена.");

            var goal = GlobalGoalContextMapper.ToGoals(new List<Records.GlobalGoal> { record }).Single();
            goal.AddStep(command.StepNumber, command.Target, command.SymbolForReward, command.AmountForReward);

            dbContext.GlobalGoalSteps.Add(Records.GlobalGoalStep.Create(
                command.GoalId,
                command.StepNumber,
                command.Target,
                command.SymbolForReward,
                command.AmountForReward));

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Step {StepNumber} added to global goal {GoalId} (target {Target})",
                command.StepNumber, command.GoalId, command.Target);

            return Result.Ok();
        }, logger, nameof(GlobalGoalCreationService));
    }
}