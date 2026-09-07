using System.Text;
using ArkWallet.Application.Contracts.GlobalGoalServices;

namespace ArkWallet.Infrastructure.Wizard;

/// <summary>
/// Форматирование списка глобальных целей в красивое сообщение с прогресс-барами.
/// </summary>
internal static class GlobalGoalFormatter
{
    private const int ProgressBarWidth = 16;

    internal static string FormatGoals(List<GlobalGoalInfo> goals)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎯 Глобальные цели");
        sb.AppendLine();

        for (var i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];

            sb.AppendLine($"{i + 1}. {goal.Name}");
            if (!string.IsNullOrWhiteSpace(goal.Description))
                sb.AppendLine($"📝 {goal.Description}");

            sb.AppendLine($"🎯 Цель: {goal.Target:N0}");
            sb.AppendLine($"📈 Достигнуто: {goal.Actual:N0} ({Percent(goal.Progress)}%)");
            sb.AppendLine();
            sb.AppendLine(ProgressBar(goal.Progress) + $" {Percent(goal.Progress)}%");
            sb.AppendLine($"🏅 Достижений: {goal.AchievedCount}");

            var nextStep = goal.Steps.FirstOrDefault(s => s.StepNumber == goal.AchievedCount + 1);
            if (nextStep is not null)
            {
                if (!string.IsNullOrEmpty(nextStep.SymbolForReward) && nextStep.AmountForReward > 0)
                    sb.AppendLine($"🎁 Награда: {nextStep.AmountForReward:N0} {nextStep.SymbolForReward}");
                sb.AppendLine($"⭐ Следующий рубеж: {nextStep.Target:N0}");
            }

            if (i < goals.Count - 1)
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");
        }

        return sb.ToString().TrimEnd();
    }

    internal static string ProgressBar(decimal progress)
    {
        var capped = Math.Clamp(progress, 0m, 1m);
        var filled = (int)Math.Round(capped * ProgressBarWidth);
        return new string('█', filled) + new string('░', ProgressBarWidth - filled);
    }

    private static int Percent(decimal progress)
        => (int)Math.Round(Math.Clamp(progress, 0m, 1m) * 100);
}