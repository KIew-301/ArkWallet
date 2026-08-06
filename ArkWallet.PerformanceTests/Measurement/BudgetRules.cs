namespace ArkWallet.PerformanceTests.Measurement;

internal static class BudgetRules
{
    public const double Margin = 1.05;
    public const int TimeFloorMs = 25;

    public static int Next(double measured) => (int)Math.Ceiling(measured * Margin);

    public static int NextTime(double measured) => Math.Max(TimeFloorMs, Next(measured));

    public static int NextRows(double measured) => Next(measured);

    public static int NextFromHistory(IEnumerable<double> recentValues)
        => recentValues.Any() ? Next(recentValues.Max()) : 0;

    public static int NextTimeFromHistory(IEnumerable<double> recentValues)
        => recentValues.Any() ? NextTime(recentValues.Max()) : 0;

    public static int NextRowsFromHistory(IEnumerable<double> recentValues)
        => recentValues.Any() ? NextRows(recentValues.Max()) : 0;
}
