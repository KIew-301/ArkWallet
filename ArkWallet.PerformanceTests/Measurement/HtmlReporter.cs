using System.Net;
using System.Text;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class HtmlReporter
{
    private static readonly (double Position, (int R, int G, int B) Color)[] GradientStops =
    {
        (0.10, (46, 160, 67)),
        (0.50, (255, 200, 0)),
        (0.90, (255, 0, 0)),
        (1.00, (0, 0, 0)),
    };

    public static string SaveSummary(string directory, RunReport run)
    {
        Directory.CreateDirectory(directory);
        var html = Build(DateTime.UtcNow, run);
        var path = Path.Combine(directory, "summary.html");
        File.WriteAllText(path, html);
        return path;
    }

    private static string Build(DateTime generatedAt, RunReport run)
    {
        var byId = run.Scenarios.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        var cards = new List<string>();
        var passed = 0;
        var failed = 0;
        var noBudget = 0;
        var notRun = 0;

        foreach (var definition in ScenarioCatalog.All)
        {
            byId.TryGetValue(definition.Id, out var scenario);
            if (scenario == null)
            {
                notRun++;
            }
            else if (!scenario.QueryBudget.HasValue)
            {
                noBudget++;
            }
            else if (IsPassing(scenario))
            {
                passed++;
            }
            else
            {
                failed++;
            }

            cards.Add(RenderCard(definition, scenario));
        }

        var total = ScenarioCatalog.All.Count;
        var implemented = ScenarioCatalog.All.Count(d => d.Implemented);
        var e2e = ScenarioCatalog.All.Count(d => !d.Implemented);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine($"<title>ArkWallet — Performance report ({generatedAt:yyyy-MM-dd HH:mm:ss} UTC)</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<header>");
        sb.AppendLine("<h1>ArkWallet — сводный отчёт по производительности</h1>");
        sb.AppendLine($"<p class=\"meta\">Сформирован: {generatedAt:yyyy-MM-dd HH:mm:ss} UTC · запуск: {run.Timestamp:yyyy-MM-dd HH:mm:ss} UTC · сценариев в запуске: {run.Scenarios.Count}</p>");
        sb.AppendLine("<div class=\"chips\">");
        sb.AppendLine($"<span class=\"chip\">Сценариев: <b>{total}</b></span>");
        sb.AppendLine($"<span class=\"chip\">Сервис-гейтов: <b>{implemented}</b></span>");
        sb.AppendLine($"<span class=\"chip\">E2E (запланированы): <b>{e2e}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-ok\">Успешно: <b>{passed}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-bad\">Нарушение бюджета: <b>{failed}</b></span>");
        sb.AppendLine($"<span class=\"chip\">Без бюджета: <b>{noBudget}</b></span>");
        sb.AppendLine($"<span class=\"chip chip-warn\">Не запущены: <b>{notRun}</b></span>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"legend\">");
        sb.AppendLine("<span class=\"badge badge-ok\">PASS</span> <span>в пределах бюджета</span>");
        sb.AppendLine("<span class=\"badge badge-bad\">FAIL</span> <span>бюджет превышен</span>");
        sb.AppendLine("<span class=\"badge badge-warn\">NOT RUN</span> <span>сценарий не выполнялся</span>");
        sb.AppendLine("<span class=\"badge badge-warn\">NOT IMPLEMENTED</span> <span>сценарий ещё не реализован</span>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"legend\"><span>Полоса бюджета:</span></div>");
        sb.AppendLine("<div class=\"gradient-legend\">");
        sb.AppendLine("<div class=\"gradient-bar\"></div>");
        sb.AppendLine("<div class=\"gradient-labels\"><span>0</span><span>10%</span><span>50%</span><span>90%</span><span>100%</span></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</header>");

        foreach (var kind in new[] { "Сервис", "E2E" })
        {
            sb.AppendLine($"<h2>{kind}</h2>");
            foreach (var card in cards.Where((_, i) => ScenarioCatalog.All[i].Kind == kind))
                sb.AppendLine(card);
        }

        sb.AppendLine(RenderProposals());

        sb.AppendLine("<footer>Отчёт строится только по repeat-прогону (медиана из N замеров, N = <code>ARKWALLET_PERF_REPEAT</code>); одиночные прогоны гейтов не пишут отчёт. Архив JSON — один файл на прогон в <code>Reports/archive</code>; сравнение прогонов — <code>overview.html</code> в той же папке запуска.</footer>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string RenderCard(ScenarioDefinition definition, RunScenario? scenario)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine("<div class=\"card-head\">");
        sb.AppendLine($"<span class=\"card-title\">{Esc(definition.Title)}</span>");
        sb.AppendLine($"<code class=\"scenario-id\">{Esc(definition.Id)}</code>");
        sb.AppendLine(RenderStatusBadge(definition, scenario));
        sb.AppendLine("</div>");
        sb.AppendLine($"<p class=\"card-desc\">{Esc(definition.Description)}</p>");

        sb.AppendLine("<div class=\"grid\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<div class=\"subtitle\">Условия сценария</div>");
        sb.AppendLine("<table class=\"conditions\">");
        foreach (var (key, value) in definition.Conditions)
            sb.AppendLine($"<tr><td>{Esc(key)}</td><td>{Esc(value)}</td></tr>");
        if (definition.Conditions.Count == 0)
            sb.AppendLine("<tr><td colspan=\"2\">условия не заданы</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div>");
        sb.AppendLine("<div class=\"subtitle\">Метрики</div>");
        if (scenario == null)
        {
            sb.AppendLine("<p class=\"placeholder\">Сценарий не выполнялся в этом запуске — метрик нет.</p>");
        }
        else
        {
            var repeatInfo = scenario.Repeats > 0
                ? $" · повторов: {scenario.Repeats} (медиана)"
                : "";
            sb.AppendLine($"<p class=\"meta\">значения{repeatInfo}</p>");
            sb.AppendLine(RenderMetric("Запросы SQL", scenario.Queries, scenario.QueryBudget, null));
            sb.AppendLine(RenderMetric("Строки (прочитано)", scenario.Rows, scenario.RowsBudget, null));
            sb.AppendLine(RenderMetric("Время", scenario.TotalMs, scenario.TimeBudget, null));
            if (scenario.Counters is { Count: > 0 })
                sb.AppendLine($"<p class=\"meta\">{string.Join(" · ", scenario.Counters.Select(c => $"{Esc(c.Name)}: {c.Value}"))}</p>");
        }
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        if (scenario is { Steps.Count: > 0 })
        {
            sb.AppendLine("<div class=\"subtitle\">Шаги</div>");
            sb.AppendLine("<table class=\"steps\">");
            sb.AppendLine("<tr><th>Шаг</th><th class=\"num\">ms</th><th class=\"num\">SQL</th><th class=\"num\">строк</th></tr>");
            foreach (var step in scenario.Steps)
                sb.AppendLine($"<tr><td>{Esc(step.Name)}</td><td class=\"num\">{Fmt(step.Ms)}</td><td class=\"num\">{step.Queries}</td><td class=\"num\">{step.Rows}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string RenderStatusBadge(ScenarioDefinition definition, RunScenario? scenario)
    {
        if (!definition.Implemented)
            return "<span class=\"badge badge-warn\">NOT IMPLEMENTED</span>";
        if (scenario == null)
            return "<span class=\"badge badge-warn\">NOT RUN</span>";
        if (!scenario.QueryBudget.HasValue)
            return "<span class=\"badge badge-warn\">NO BUDGET</span>";

        return IsPassing(scenario)
            ? "<span class=\"badge badge-ok\">PASS</span>"
            : "<span class=\"badge badge-bad\">FAIL</span>";
    }

    private static bool IsPassing(RunScenario scenario)
        => scenario.QueryBudget.HasValue
            && scenario.TimeBudget.HasValue
            && scenario.Queries <= scenario.QueryBudget.Value
            && (!scenario.RowsBudget.HasValue || scenario.Rows <= scenario.RowsBudget.Value)
            && scenario.TotalMs <= scenario.TimeBudget.Value;

    private static string RenderMetric(string label, double value, int? budget, string? extra)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"metric\">");
        sb.AppendLine($"<div class=\"metric-head\"><span>{Esc(label)}</span><span class=\"metric-value\">{Fmt(value)}</span></div>");

        if (budget.HasValue)
        {
            var fraction = budget.Value > 0 ? value / budget.Value : 0;
            var width = Math.Min(100.0, fraction * 100.0);
            var color = GradientColor(fraction);
            sb.AppendLine($"<div class=\"bar\"><div class=\"bar-fill\" style=\"width:{Fmt(width)}%;background:{color}\"></div></div>");
            sb.AppendLine($"<div class=\"metric-sub\">бюджет {budget.Value} · {Fmt(value)} / {budget.Value} ({Fmt(fraction * 100)}%)</div>");
        }
        else
        {
            sb.AppendLine("<div class=\"metric-sub\">без бюджета</div>");
        }

        if (extra != null)
            sb.AppendLine($"<div class=\"metric-sub\">{extra}</div>");

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string RenderProposals()
    {
        var byId = RunArchive.ReadAll()
            .SelectMany(r => r.Scenarios)
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("<h2>Предлагаемые бюджеты (правило: последние замеры × 1.05)</h2>");
        sb.AppendLine("<p class=\"card-desc\">Бюджет = максимум за последние прогоны +5% (время — не ниже 25 ms из-за шума). После оптимизаций (#10-20) применить новые значения к <code>Gates/GateBudgets.cs</code>.</p>");
        sb.AppendLine("<table class=\"proposal\">");
        sb.AppendLine("<tr><th>Сценарий</th><th class=\"num\">Прогонов</th><th class=\"num\">Запросы (замер → +5%)</th><th class=\"num\">Строки (замер → +5%)</th><th class=\"num\">Время ms (замер → +5%)</th></tr>");

        foreach (var definition in ScenarioCatalog.All.Where(d => d.Implemented))
        {
            if (!byId.TryGetValue(definition.Id, out var history))
            {
                sb.AppendLine($"<tr><td>{Esc(definition.Id)}</td><td class=\"num\">0</td><td colspan=\"3\">нет замеров</td></tr>");
                continue;
            }

            var maxQueries = history.Max(h => h.Queries);
            var maxTime = history.Max(h => h.TotalMs);
            var rowValues = history.Select(h => h.Rows).Where(r => r > 0).ToArray();
            var rowsCell = rowValues.Length > 0
                ? $"{Fmt(rowValues.Max())} → {BudgetRules.NextRows(rowValues.Max())}"
                : "—";
            sb.AppendLine(
                $"<tr><td>{Esc(definition.Id)}</td><td class=\"num\">{history.Length}</td>" +
                $"<td class=\"num\">{Fmt(maxQueries)} → {BudgetRules.Next(maxQueries)}</td>" +
                $"<td class=\"num\">{rowsCell}</td>" +
                $"<td class=\"num\">{Fmt(maxTime)} → {BudgetRules.NextTime(maxTime)}</td></tr>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string GradientColor(double fraction)
    {
        if (fraction <= GradientStops[0].Position)
            return "rgb(46,160,67)";
        if (fraction >= GradientStops[^1].Position)
            return "rgb(0,0,0)";

        for (int i = 0; i < GradientStops.Length - 1; i++)
        {
            var (p0, c0) = GradientStops[i];
            var (p1, c1) = GradientStops[i + 1];
            if (fraction <= p1)
            {
                var t = (fraction - p0) / (p1 - p0);
                var r = (int)Math.Round(c0.R + (c1.R - c0.R) * t);
                var g = (int)Math.Round(c0.G + (c1.G - c0.G) * t);
                var b = (int)Math.Round(c0.B + (c1.B - c0.B) * t);
                return $"rgb({r},{g},{b})";
            }
        }

        return "rgb(0,0,0)";
    }

    private static string Esc(string value) => WebUtility.HtmlEncode(value);

    private static string Fmt(double value, string format = "0.##")
        => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private const string Css = """
        body { font-family: "Segoe UI", Arial, sans-serif; max-width: 1100px; margin: 24px auto;
               padding: 0 16px; color: #1f2328; background: #f6f8fa; font-size: 14px; }
        h1 { font-size: 22px; margin: 0 0 4px; }
        h2 { font-size: 18px; margin: 28px 0 12px; border-bottom: 2px solid #d0d7de; padding-bottom: 6px; }
        .meta { color: #57606a; margin: 0 0 12px; }
        .chips { margin: 10px 0; display: flex; flex-wrap: wrap; gap: 8px; }
        .chip { background: #fff; border: 1px solid #d0d7de; border-radius: 999px; padding: 4px 12px; }
        .chip-ok { border-color: #2da44e; color: #1a7f37; }
        .chip-bad { border-color: #cf222e; color: #cf222e; }
        .chip-warn { border-color: #9a6700; color: #9a6700; }
        .legend { display: flex; flex-wrap: wrap; gap: 14px; align-items: center; color: #57606a; margin: 8px 0 2px; }
        .gradient-legend { max-width: 520px; margin: 4px 0 16px; }
        .gradient-bar { height: 10px; border-radius: 4px;
                        background: linear-gradient(90deg, #2ea44e 0%, #2ea44e 10%, #ffc800 50%, #ff0000 90%, #000000 100%); }
        .gradient-labels { display: flex; justify-content: space-between; font-size: 11px; color: #57606a; margin-top: 2px; }
        .card { background: #fff; border: 1px solid #d0d7de; border-radius: 8px; padding: 14px 18px; margin: 12px 0; }
        .card-head { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
        .card-title { font-weight: 600; font-size: 15px; }
        .scenario-id { background: #f0f3f6; border: 1px solid #d0d7de; border-radius: 4px; padding: 2px 6px; font-size: 12px; }
        .card-desc { color: #57606a; margin: 6px 0 12px; }
        .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
        @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
        .subtitle { font-size: 12px; text-transform: uppercase; letter-spacing: .04em; color: #57606a; margin: 8px 0 6px; }
        table { border-collapse: collapse; width: 100%; }
        td, th { padding: 4px 8px; border-bottom: 1px solid #eaeef2; text-align: left; }
        td:last-child { color: #57606a; }
        .num { text-align: right; }
        th.num { text-align: right; }
        .proposal td, .proposal th { border-bottom: 1px solid #d0d7de; }
        .metric { margin: 10px 0; }
        .metric-head { display: flex; justify-content: space-between; font-weight: 600; }
        .metric-value { font-variant-numeric: tabular-nums; }
        .metric-sub { color: #57606a; font-size: 12px; margin-top: 2px; }
        .bar { background: #eaeef2; border-radius: 4px; height: 8px; margin: 6px 0 2px; overflow: hidden; }
        .bar-fill { height: 100%; }
        .badge { border-radius: 4px; padding: 2px 8px; font-size: 12px; font-weight: 600; color: #fff; }
        .badge-ok { background: #2da44e; }
        .badge-bad { background: #cf222e; }
        .badge-warn { background: #bf8700; }
        .placeholder { color: #9a6700; font-style: italic; margin: 6px 0; }
        footer { margin: 30px 0 10px; color: #57606a; font-size: 12px; }
        """;
}
