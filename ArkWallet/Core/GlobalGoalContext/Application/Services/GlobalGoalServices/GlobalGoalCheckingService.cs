using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;
using ArkWallet.Core.General.Domain.Common;
using GlobalGoal = ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal.GlobalGoal;
using ArkWallet.Core.GlobalGoalContext.Domain.Events;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Records = global::ArkWallet.Infrastructure.Data;

namespace ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

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
                    .AsSplitQuery()
                    .Include(g => g.Histories)
                    .Include(g => g.Steps)
                    .ToListAsync();

                var context = GlobalGoalContextMapper.ToGoals(goals);

                foreach (var goal in context)
                {
                    goal.SetEventPublisher(eventPublisher);
                    await UpdateActualAsync(goal);

                    var historyCountBefore = goal.HistoryCount;
                    await goal.CheckGoal(timeProvider);

                    if (goal.History.Count > historyCountBefore)
                        logger.LogInformation("Global goal achieved: {Name} (achieved {Count} times)", goal.Name, goal.AchievedCount);
                }

                SyncState(goals, context);
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

    private void SyncState(List<Records.GlobalGoal> goals, List<GlobalGoal> context)
    {
        GlobalGoalContextMapper.SyncGoalsToRecords(goals, context, dbContext);
    }
}
