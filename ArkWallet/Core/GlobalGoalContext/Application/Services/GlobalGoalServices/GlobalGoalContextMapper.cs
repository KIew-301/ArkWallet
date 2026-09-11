using ArkWallet.Core.GlobalGoalContext.Domain.GlobalGoal;
using ArkWallet.Core.GlobalGoalContext.Domain.Events;
using ArkWallet.Core.General.Domain.Common;
using Records = global::ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;

/// <summary>
/// Маппинг между записями БД и агрегатом GlobalGoal контекста глобальных целей.
/// </summary>
internal static class GlobalGoalContextMapper
{
    internal static List<GlobalGoal> ToGoals(List<Records.GlobalGoal> goals)
    {
        return goals
            .Select(g => GlobalGoal.Load(new GlobalGoalAggregateData(
                g.Id,
                g.Name,
                g.Description,
                g.Target,
                g.Actual,
                g.AchievedCount,
                (g.Histories ?? new List<Records.GlobalGoalHistory>())
                    .Select(h => new GlobalGoalHistory(
                        h.AchievedAt,
                        h.Target,
                        h.SymbolForReward,
                        h.AmountForReward))
                    .ToList(),
                (g.Steps ?? new List<Records.GlobalGoalStep>())
                    .Select(s => new GlobalGoalStep(
                        s.StepNumber,
                        s.Target,
                        s.SymbolForReward,
                        s.AmountForReward))
                    .ToList())))
            .ToList();
    }

    internal static void SyncGoalsToRecords(
        List<Records.GlobalGoal> goals,
        List<GlobalGoal> context,
        Records.ArkWalletDbContext dbContext)
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
