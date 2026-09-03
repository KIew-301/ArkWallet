using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.GlobalGoalServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.GlobalGoalContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.GlobalGoalServices;

internal class GlobalGoalCheckingService(
    ArkWalletDbContext dbContext,
    IEnumerable<IDomainGlobalGoalCalculation> calculations,
    IEventPublisher eventPublisher,
    ILogger<GlobalGoalCheckingService> logger,
    TimeProvider timeProvider) : IGlobalGoalCheckingService
{
    public async Task<Result> CheckGoalsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var goals = await dbContext.GlobalGoals
                    .Include(g => g.Histories)
                    .Include(g => g.Steps)
                    .ToListAsync();

                var context = GlobalGoalContextMapper.ToGoals(goals);

                foreach (var goal in context)
                {
                    goal.SetEventPublisher(eventPublisher);
                    await UpdateActualAsync(goal);

                    var historyCountBefore = goal.History.Count;
                    await goal.CheckGoal(timeProvider);

                    if (goal.History.Count > historyCountBefore)
                        logger.LogInformation("Global goal achieved: {Name} (achieved {Count} times)", goal.Name, goal.AchievedCount);
                }

                SyncGoalState(goals, context);
                await dbContext.SaveChangesAsync();

                return Result.Ok();
            });
        }, logger, nameof(GlobalGoalCheckingService));
    }

    private async Task UpdateActualAsync(GlobalGoal goal)
    {
        var calculation = calculations.FirstOrDefault(c => c.GoalName == goal.Name);
        if (calculation is null)
            return;

        goal.UpdateActual(await calculation.CalculateAsync(dbContext));
    }

    private void SyncGoalState(List<Records.GlobalGoal> goals, List<GlobalGoal> context)
    {
        foreach (var goal in context)
        {
            var record = goals.First(g => g.Id == goal.Id);
            record.Actual = goal.Actual;
            record.Target = goal.Target;
            record.Progress = goal.Progress;
            record.AchievedCount = goal.AchievedCount;

            foreach (var entry in goal.History)
            {
                var alreadySaved = record.Histories.Any(h => h.AchievedAt == entry.AchievedAt);

                if (!alreadySaved)
                {
                    dbContext.GlobalGoalHistories.Add(Records.GlobalGoalHistory.Create(
                        goal.Id, entry.AchievedAt, entry.Target, entry.SymbolForReward, entry.AmountForReward));
                }
            }
        }
    }
}
