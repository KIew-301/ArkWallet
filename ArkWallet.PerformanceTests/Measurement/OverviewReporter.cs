using System.Globalization;
using System.Net;
using System.Text;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class OverviewReporter
{
    private const double QueryDeltaThreshold = 2.0;
    private const double RowsDeltaThreshold = 2.0;
    private const double QueryRegressionMinPercent = 10.0;
    private const double QueryRegressionMinCount = 10.0;
    private const double TimeColorFloorMs = 10.0;
    private const double TimeImprovementFloorMs = 20.0;
    private const double TimeImprovementMinPercent = 60.0;

    public static void Save(string directory, RunReport current, IReadOnlyList<RunReport> baselineRuns, string? baselineLabel = null)
    {
        Directory.CreateDirectory(directory);
        var html = Build(DateTime.UtcNow, current, baselineRuns, baselineLabel);
        File.WriteAllText(Path.Combine(directory, "overview.html"), html);
    }

    private static string Build(DateTime generatedAt, RunReport current, IReadOnlyList<RunReport> baselineRuns, string? baselineLabel)
    {
        var latestPrevious = baselineRuns.FirstOrDefault();

        var rows = new List<string>();
        var compared = 0;
        var newScenarios = 0;
        var improved = 0;
        var regressed = 0;
        var stable = 0;
        double baselineTotalQueries = 0;
        double currentTotalQueries = 0;
        double baselineTotalRows = 0;
        double currentTotalRows = 0;
        double baselineTotalMs = 0;
        double currentTotalMs = 0;

        foreach (var scenario in current.Scenarios.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            var prev = FindBaseline(baselineRuns, scenario.Id);
            if (prev == null)
            {
                newScenarios++;
                rows.Add(RenderRow(scenario, null, scenario.Queries, null, null, scenario.Rows, null, null, scenario.TotalMs, null, "no-data", true));
                continue;
            }

            compared++;
            var dq = Delta(prev.Queries, scenario.Queries);
            var dr = Delta(prev.Rows, scenario.Rows);
            var dt = Delta(prev.TotalMs, scenario.TotalMs);
            var timeMeasurable = prev.TotalMs >= TimeColorFloorMs;
            var status = Classify(dq, dr, scenario.Queries, prev.TotalMs, dt);

            if (status == "improved") improved++;
            else if (status == "regressed") regressed++;
            else stable++;

            baselineTotalQueries += prev.Queries;
            currentTotalQueries += scenario.Queries;
            baselineTotalRows += prev.Rows;
            currentTotalRows += scenario.Rows;
            baselineTotalMs += prev.TotalMs;
            currentTotalMs += scenario.TotalMs;

            rows.Add(RenderRow(scenario, prev.Queries, scenario.Queries, dq, prev.Rows, scenario.Rows, dr, prev.TotalMs, scenario.TotalMs, dt, status, timeMeasurable));
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<title>ArkWallet — Обзор изменений производительности</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<header>");
        sb.AppendLine("<h1>ArkWallet — общая картина по производительности</h1>");
        sb.AppendLine($"<p class=\"meta\">Сформирован: {generatedAt:yyyy-MM-dd HH:mm:ss} UTC · прогон: {current.Timestamp:yyyy-MM-dd HH:mm:ss} UTC" +
            (baselineLabel != null
                ? $" · база: выбранный прогон {baselineLabel} (Reports/target.txt)"
                : latestPrevious != null
                    ? $" · база: последний прогон, в котором есть сценарий (прогонов в архиве: {baselineRuns.Count})"
                    : " · предыдущих прогонов нет — сравнивать не с чем") + "</p>");
        sb.AppendLine("<div class=\"chips\">");
        sb.AppendLine($"<span class=\"chip\">Сценариев в прогоне: <b>{current.Scenarios.Count}</b></span>");
        sb.AppendLine($"<span class=\"chip\">Сравниваемых: <b>{compared}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-warn\">Новых (нет в истории): <b>{newScenarios}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-ok\">Улучшено: <b>{improved}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-warn\">Стабильно: <b>{stable}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-bad\">Регресс: <b>{regressed}</b></span>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"totals\">");
        if (compared > 0)
        {
            sb.AppendLine($"<div class=\"total\"><div class=\"total-label\">Запросы SQL (сумма)</div>" +
                $"<div class=\"total-value\">{Fmt(baselineTotalQueries)} → {Fmt(currentTotalQueries)} <span class=\"{DeltaClass(Delta(baselineTotalQueries, currentTotalQueries))}\">{FmtDelta(Delta(baselineTotalQueries, currentTotalQueries))}</span></div></div>");
            sb.AppendLine($"<div class=\"total\"><div class=\"total-label\">Строки (сумма)</div>" +
                $"<div class=\"total-value\">{Fmt(baselineTotalRows)} → {Fmt(currentTotalRows)} <span class=\"{DeltaClass(Delta(baselineTotalRows, currentTotalRows))}\">{FmtDelta(Delta(baselineTotalRows, currentTotalRows))}</span></div></div>");
            sb.AppendLine($"<div class=\"total\"><div class=\"total-label\">Время, мс (сумма)</div>" +
                $"<div class=\"total-value\">{Fmt(baselineTotalMs)} → {Fmt(currentTotalMs)} <span class=\"{DeltaClass(Delta(baselineTotalMs, currentTotalMs))}\">{FmtDelta(Delta(baselineTotalMs, currentTotalMs))}</span></div></div>");
        }
        else
        {
            sb.AppendLine(baselineLabel != null
                ? "<p class=\"placeholder\">В выбранном целевом прогоне нет ни одного сценария из текущего прогона — сравнивать не с чем.</p>"
                : "<p class=\"placeholder\">Нет предыдущего прогона — сравнивать не с чем. Запустите прогон ещё раз, чтобы увидеть дельту.</p>");
        }
        sb.AppendLine("</div>");
        sb.AppendLine("</header>");

        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Сценарий</th><th class=\"num\">SQL было</th><th class=\"num\">SQL стало</th><th class=\"num\">Δ SQL</th><th class=\"num\">строк было</th><th class=\"num\">строк стало</th><th class=\"num\">Δ строк</th><th class=\"num\">Время было, мс</th><th class=\"num\">Время стало, мс</th><th class=\"num\">Δ время</th><th>Статус</th></tr>");
        foreach (var row in rows)
            sb.AppendLine(row);
        sb.AppendLine("</table>");

        sb.AppendLine("<footer>База сравнения: последний прогон в <code>Reports/archive</code>, где есть сценарий, либо выбранный целевой прогон (переменная <code>ARKWALLET_PERF_TARGET</code>, сохраняется в <code>Reports/target.txt</code> до смены). Сценарии без замера в базе помечаются «Нет данных». Статус определяется только по детерминированным метрикам: запросы ±2%, строки ±2%; рост запросов не считается регрессом, если прирост &lt;10% или запросов &lt;10. «Улучшено» также ставится, если время было &gt;20 мс и снизилось на &ge;60%. Время (±20% для флоу &ge;10 мс) показывается справочно и на статус не влияет. Repeat-прогон (медиана из N замеров): <code>ARKWALLET_PERF_REPEAT=100 dotnet test ArkWallet.PerformanceTests --filter \"FullyQualifiedName~Repeats\"</code>.</footer>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static RunScenario? FindBaseline(IReadOnlyList<RunReport> previousRuns, string scenarioId)
    {
        foreach (var run in previousRuns)
        {
            foreach (var scenario in run.Scenarios)
            {
                if (string.Equals(scenario.Id, scenarioId, StringComparison.OrdinalIgnoreCase))
                    return scenario;
            }
        }

        return null;
    }

    private static string RenderRow(
        RunScenario scenario, double? bq, double? cq, double? dq,
        double? br, double? cr, double? dr,
        double? bms, double? cms, double? dt, string status, bool timeMeasurable)
    {
        var badge = status switch
        {
            "improved" => "<span class=\"badge badge-ok\">Улучшено</span>",
            "regressed" => "<span class=\"badge badge-bad\">Регресс</span>",
            "no-data" => "<span class=\"badge badge-warn\">Нет данных</span>",
            _ => "<span class=\"badge badge-warn\">Стабильно</span>"
        };

        var note = status == "no-data" ? " <span class=\"note\">(нет в истории)</span>" : "";

        return $"<tr><td>{Esc(scenario.Title)} <code class=\"scenario-id\">{Esc(scenario.Id)}</code>{note}</td>" +
            $"<td class=\"num\">{Fmt(bq)}</td><td class=\"num\">{Fmt(cq)}</td><td class=\"num {DeltaClass(dq)}\">{FmtDelta(dq)}</td>" +
            $"<td class=\"num\">{Fmt(br)}</td><td class=\"num\">{Fmt(cr)}</td><td class=\"num {DeltaClass(dr)}\">{FmtDelta(dr)}</td>" +
            $"<td class=\"num\">{Fmt(bms)}</td><td class=\"num\">{Fmt(cms)}</td><td class=\"num {DeltaClass(dt, timeMeasurable)}\">{FmtDelta(dt)}</td>" +
            $"<td>{badge}</td></tr>";
    }

    private static string Classify(double? dq, double? dr, double currentQueries, double prevMs, double? dt)
    {
        var isImproved = (dq ?? 0) <= -QueryDeltaThreshold
            || (dr ?? 0) <= -RowsDeltaThreshold
            || (prevMs > TimeImprovementFloorMs && (dt ?? 0) <= -TimeImprovementMinPercent);

        var queryCountsAsRegression = (dq ?? 0) >= QueryRegressionMinPercent && currentQueries >= QueryRegressionMinCount;
        var isRegressed = (dr ?? 0) >= RowsDeltaThreshold || queryCountsAsRegression;

        if (isImproved == isRegressed)
            return "stable";

        return isImproved ? "improved" : "regressed";
    }

    private static double? Delta(double baseline, double current)
        => baseline > 0 ? (current - baseline) / baseline * 100.0 : null;

    private static string DeltaClass(double? d, bool timeMeasurable = true)
        => !timeMeasurable ? "" : d.HasValue && d.Value <= -0.05 ? "delta-good" : d.HasValue && d.Value >= 0.05 ? "delta-bad" : "";

    private static string FmtDelta(double? d)
        => d.HasValue ? (d.Value >= 0 ? "+" : "") + Fmt(d.Value) + "%" : "—";

    private static string Fmt(double? value)
        => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "—";

    private static string Fmt(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Esc(string value) => WebUtility.HtmlEncode(value);

    private const string Css = """
        body { font-family: "Segoe UI", Arial, sans-serif; max-width: 1000px; margin: 24px auto;
               padding: 0 16px; color: #1f2328; background: #f6f8fa; font-size: 14px; }
        h1 { font-size: 22px; margin: 0 0 4px; }
        .meta { color: #57606a; margin: 0 0 12px; }
        .chips { margin: 10px 0; display: flex; flex-wrap: wrap; gap: 8px; }
        .chip { background: #fff; border: 1px solid #d0d7de; border-radius: 999px; padding: 4px 12px; }
        .chip-ok { border-color: #2da44e; color: #1a7f37; }
        .chip-warn { border-color: #9a6700; color: #9a6700; }
        .chip-bad { border-color: #cf222e; color: #cf222e; }
        .totals { display: flex; gap: 24px; margin: 14px 0; flex-wrap: wrap; }
        .total { background: #fff; border: 1px solid #d0d7de; border-radius: 8px; padding: 10px 16px; }
        .total-label { font-size: 12px; text-transform: uppercase; letter-spacing: .04em; color: #57606a; }
        .total-value { font-size: 18px; font-weight: 600; margin-top: 4px; font-variant-numeric: tabular-nums; }
        table { border-collapse: collapse; width: 100%; background: #fff; border: 1px solid #d0d7de; border-radius: 8px; overflow: hidden; }
        td, th { padding: 6px 10px; border-bottom: 1px solid #eaeef2; text-align: left; }
        .num { text-align: right; font-variant-numeric: tabular-nums; }
        .scenario-id { background: #f0f3f6; border-radius: 4px; padding: 1px 5px; font-size: 12px; }
        .note { color: #9a6700; font-size: 12px; }
        .badge { border-radius: 4px; padding: 2px 8px; font-size: 12px; font-weight: 600; color: #fff; white-space: nowrap; }
        .badge-ok { background: #2da44e; }
        .badge-bad { background: #cf222e; }
        .badge-warn { background: #bf8700; }
        .delta-good { color: #1a7f37; font-weight: 600; }
        .delta-bad { color: #cf222e; font-weight: 600; }
        .placeholder { color: #9a6700; font-style: italic; margin: 6px 0; }
        footer { margin: 30px 0 10px; color: #57606a; font-size: 12px; }
        """;
}
