using System.Diagnostics.CodeAnalysis;
using System.Text;
using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Other;

namespace ArkWallet.Presentation.Health;

[ExcludeFromCodeCoverage(Justification = "Метрики: инфраструктурный код форматирования снимка метрик, не содержит бизнес-логики")]
internal sealed class MetricsSnapshotService : IMetricsSnapshotService
{
    public Task<string> GetMetricsTextAsync()
    {
        var sb = new StringBuilder();

        var serviceCounts = ArkWalletMetrics.GetServiceResultCounts().ToList();
        sb.AppendLine("== Service results ==");
        if (serviceCounts.Count == 0)
            sb.AppendLine("  (нет данных)");
        else
            foreach (var (service, counts) in serviceCounts)
                sb.AppendLine($"  {service}: ok={counts.Ok}, fail={counts.Fail}");

        var lastErrors = ArkWalletMetrics.GetLastErrors().ToList();
        if (lastErrors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("== Last errors ==");
            foreach (var (service, error) in lastErrors)
                sb.AppendLine($"  {service}: {error}");
        }

        var lockStats = ArkWalletMetrics.GetLockWaitStats().ToList();
        sb.AppendLine();
        sb.AppendLine("== Lock wait (seconds) ==");
        if (lockStats.Count == 0)
            sb.AppendLine("  (нет данных)");
        else
            foreach (var (lockType, stats) in lockStats)
                sb.AppendLine($"  {lockType}: count={stats.Count}, avg={stats.Average:F4}, max={stats.Max:F4}");

        var commandStats = ArkWalletMetrics.GetCommandStats().ToList();
        sb.AppendLine();
        sb.AppendLine("== Bot commands ==");
        if (commandStats.Count == 0)
            sb.AppendLine("  (нет данных)");
        else
            foreach (var (command, stats) in commandStats)
                sb.AppendLine($"  {command}: count={stats.Count}, avg={stats.Average:F4}s, max={stats.Max:F4}s");

        return Task.FromResult(sb.ToString());
    }
}
