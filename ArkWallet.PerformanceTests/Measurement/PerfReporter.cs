namespace ArkWallet.PerformanceTests.Measurement;

internal static class PerfReporter
{
    public static string ReportsDirectory { get; } = FindReportsDirectory();

    private static string FindReportsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ArkWallet.sln")))
            {
                var provider = RepeatConfig.DatabaseProvider;
                return Path.Combine(dir.FullName, "ArkWallet.PerformanceTests", "Reports", provider);
            }

            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "Reports");
    }
}
