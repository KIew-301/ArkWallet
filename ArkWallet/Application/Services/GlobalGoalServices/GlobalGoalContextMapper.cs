using ArkWallet.Domain.GlobalGoalContext;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.GlobalGoalServices;

/// <summary>
/// Маппинг между записями БД и агрегатом GlobalGoal контекста глобальных целей.
/// </summary>
internal static class GlobalGoalContextMapper
{
    internal static List<GlobalGoal> ToGoals(List<Records.GlobalGoal> goals)
    {
        return goals
            .Select(g => GlobalGoal.Load(
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
                    .ToList()))
            .ToList();
    }
}
