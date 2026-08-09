namespace ArkWallet.PerformanceTests.Measurement;

internal static class ReportTarget
{
    public const string EnvVar = "ARKWALLET_PERF_TARGET";

    public static string MarkerFile => Path.Combine(PerfReporter.ReportsDirectory, "target.txt");

    public static bool IsSet => File.Exists(MarkerFile);

    public static string? FileName
    {
        get
        {
            if (!IsSet)
                return null;

            var value = File.ReadAllText(MarkerFile).Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    public static void ConfigureFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            return;
        }

        var resolved = ResolveToFileName(value);
        if (resolved == null)
        {
            Console.WriteLine($"[perf] Предупреждение: целевой прогон не найден: {value}. База сравнения не изменится.");
            return;
        }

        File.WriteAllText(MarkerFile, resolved);
        Console.WriteLine($"[perf] Целевой прогон для сравнения сохранён: {resolved} ({MarkerFile})");
    }

    public static RunReport? GetResolved()
    {
        var name = FileName;
        if (name == null)
            return null;

        var run = RunArchive.ReadFile(name);
        if (run == null)
            Console.WriteLine($"[perf] Предупреждение: целевой прогон {name} не найден в архиве — сравнение с последним прогоном.");

        return run;
    }

    public static void Clear()
    {
        if (IsSet)
            File.Delete(MarkerFile);
        Console.WriteLine("[perf] Целевой прогон сброшен — сравнение с последним прогоном.");
    }

    private static string? ResolveToFileName(string value)
    {
        var archive = RunArchive.ArchiveDirectory;

        foreach (var candidate in new[]
                 {
                     Path.Combine(archive, value),
                     value,
                     Path.Combine(archive, Path.GetFileName(value)),
                 })
        {
            if (File.Exists(candidate))
                return Path.GetFileName(candidate);
        }

        return null;
    }
}
