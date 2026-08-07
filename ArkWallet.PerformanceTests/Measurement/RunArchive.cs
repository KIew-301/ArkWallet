using System.Text.Json;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class RunArchive
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string ArchiveDirectory => Path.Combine(PerfReporter.ReportsDirectory, "archive");

    public static void WriteRun(RunReport run)
    {
        Directory.CreateDirectory(ArchiveDirectory);
        var json = JsonSerializer.Serialize(run, WriteOptions);
        File.WriteAllText(Path.Combine(ArchiveDirectory, $"run-{run.Timestamp:yyyyMMdd-HHmmss-fff}.json"), json);
    }

    public static IReadOnlyList<RunReport> ReadAllBefore(DateTime timestamp)
        => ReadAll()
            .Where(r => r.Timestamp < timestamp)
            .OrderByDescending(r => r.Timestamp)
            .ToArray();

    public static IReadOnlyList<RunReport> ReadAll()
    {
        if (!Directory.Exists(ArchiveDirectory))
            return Array.Empty<RunReport>();

        var result = new List<RunReport>();
        foreach (var file in Directory.GetFiles(ArchiveDirectory, "run-*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<RunReport>(File.ReadAllText(file), ReadOptions) is { } run)
                    result.Add(run);
            }
            catch (JsonException)
            {
            }
        }

        return result.OrderBy(r => r.Timestamp).ToArray();
    }
}
