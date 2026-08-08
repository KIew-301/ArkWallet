using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace ArkWallet.Application.Common;

[ExcludeFromCodeCoverage(Justification = "Метрики: инфраструктурный код сбора метрик приложения, не содержит бизнес-логики")]
internal static class ArkWalletMetrics
{
    internal static readonly Meter Meter = new("ArkWallet", "1.0.0");

    internal static readonly Counter<long> ServiceResults = Meter.CreateCounter<long>(
        "arkwallet_service_results_total",
        description: "РљРѕР»РёС‡РµСЃС‚РІРѕ Ok/Fail Result РїРѕ СЃРµСЂРІРёСЃР°Рј.");

    internal static readonly Histogram<double> LockWaitSeconds = Meter.CreateHistogram<double>(
        "arkwallet_lock_wait_seconds",
        unit: "seconds",
        description: "Р’СЂРµРјСЏ РІС‹РїРѕР»РЅРµРЅРёСЏ SELECT ... FOR UPDATE (РІРєР»СЋС‡Р°СЏ РѕР¶РёРґР°РЅРёРµ Р±Р»РѕРєРёСЂРѕРІРєРё).");

    internal static readonly Counter<long> Commands = Meter.CreateCounter<long>(
        "arkwallet_commands_total",
        description: "РљРѕР»РёС‡РµСЃС‚РІРѕ РІС‹Р·РѕРІРѕРІ РєРѕРјР°РЅРґ Р±РѕС‚Р°.");

    internal static readonly Histogram<double> CommandDurationSeconds = Meter.CreateHistogram<double>(
        "arkwallet_command_duration_seconds",
        unit: "seconds",
        description: "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ РІС‹РїРѕР»РЅРµРЅРёСЏ РєРѕРјР°РЅРґ Р±РѕС‚Р°.");

    internal sealed record ServiceResultCounts(long Ok, long Fail);

    internal sealed record AggregateStats(long Count, double Sum, double Max)
    {
        public double Average => Count == 0 ? 0 : Sum / Count;
    }

    private static readonly ConcurrentDictionary<string, ServiceResultCounts> _serviceResultCounts = new();

    internal static readonly ConcurrentDictionary<string, AggregateStats> LockWaitStats = new();
    internal static readonly ConcurrentDictionary<string, AggregateStats> CommandStats = new();
    internal static readonly ConcurrentDictionary<string, string> LastErrors = new();

    internal static void RecordServiceResult(string service, bool isSuccess, string? message = null)
    {
        ServiceResults.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("result", isSuccess ? "ok" : "fail"));

        _serviceResultCounts.AddOrUpdate(service,
            isSuccess ? new ServiceResultCounts(1, 0) : new ServiceResultCounts(0, 1),
            (_, current) => isSuccess
                ? current with { Ok = current.Ok + 1 }
                : current with { Fail = current.Fail + 1 });

        if (!isSuccess && !string.IsNullOrWhiteSpace(message))
            LastErrors[service] = message;
    }

    internal static void RecordLockWait(string lockType, double seconds)
    {
        LockWaitSeconds.Record(seconds, new KeyValuePair<string, object?>("lock_type", lockType));

        LockWaitStats.AddOrUpdate(lockType,
            new AggregateStats(1, seconds, seconds),
            (_, current) => new AggregateStats(current.Count + 1, current.Sum + seconds, Math.Max(current.Max, seconds)));
    }

    internal static void RecordCommand(string command, double seconds)
    {
        Commands.Add(1, new KeyValuePair<string, object?>("command", command));
        CommandDurationSeconds.Record(seconds, new KeyValuePair<string, object?>("command", command));

        CommandStats.AddOrUpdate(command,
            new AggregateStats(1, seconds, seconds),
            (_, current) => new AggregateStats(current.Count + 1, current.Sum + seconds, Math.Max(current.Max, seconds)));
    }

    internal static IEnumerable<KeyValuePair<string, ServiceResultCounts>> GetServiceResultCounts()
        => _serviceResultCounts.OrderBy(kv => kv.Key);

    internal static IEnumerable<KeyValuePair<string, AggregateStats>> GetLockWaitStats()
        => LockWaitStats.OrderBy(kv => kv.Key);

    internal static IEnumerable<KeyValuePair<string, AggregateStats>> GetCommandStats()
        => CommandStats.OrderBy(kv => kv.Key);

    internal static IEnumerable<KeyValuePair<string, string>> GetLastErrors()
        => LastErrors.OrderBy(kv => kv.Key);
}
