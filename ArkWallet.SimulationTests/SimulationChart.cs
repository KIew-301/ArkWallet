using System.Globalization;
using System.Text;
using ArkWallet.Domain.Entities;

namespace ArkWallet.SimulationTests;

internal static class SimulationChart
{
    private const string ChartJs = "https://unpkg.com/lightweight-charts@4.2.0/dist/lightweight-charts.standalone.production.js";

    public static string RenderAndOpen(
        string title, string subtitle, string symbol, IReadOnlyList<PriceCandle> candles, int timeframeMinutes = 5)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ArkWalletSimulation");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "simulation_" + symbol + "_" + timeframeMinutes + "m_" + DateTime.Now.ToString("HHmmssfff") + ".html");
        File.WriteAllText(path, RenderHtml(title, subtitle, symbol, candles, timeframeMinutes));
        OpenInBrowser(path);
        return path;
    }

    public static string RenderHtml(string title, string subtitle, string symbol, IReadOnlyList<PriceCandle> candles, int timeframeMinutes)
    {
        var dataJson = BuildDataJson(candles, timeframeMinutes);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='ru'>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>" + Escape(title) + "</title>");
        html.AppendLine("<style>" + BuildCss() + "</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class='card'>");
        html.AppendLine("  <h1>" + Escape(title) + "</h1>");
        html.AppendLine("  <p class='sub'>" + Escape(subtitle) + "</p>");
        html.AppendLine("  <div id='chart'></div>");
        html.AppendLine("</div>");
        html.AppendLine("<script src='" + ChartJs + "'></script>");
        html.AppendLine("<script>");
        html.AppendLine("const data = " + dataJson + ";");
        html.AppendLine("const chart = LightweightCharts.createChart(document.getElementById('chart'), {");
        html.AppendLine("  width: 1200,");
        html.AppendLine("  height: 600,");
        html.AppendLine("  layout: { background: { color: '#1c1c28' }, textColor: '#9b9bb4' },");
        html.AppendLine("  grid: { vertLines: { color: '#2a2a3c' }, horzLines: { color: '#2a2a3c' } },");
        html.AppendLine("  timeScale: { timeVisible: true, secondsVisible: false },");
        html.AppendLine("  rightPriceScale: { borderColor: '#2a2a3c' }");
        html.AppendLine("});");
        html.AppendLine("const series = chart.addCandlestickSeries({ upColor: '#26a69a', downColor: '#ef5350', borderVisible: false, wickUpColor: '#26a69a', wickDownColor: '#ef5350' });");
        html.AppendLine("series.setData(data);");
        html.AppendLine("chart.timeScale().fitContent();");
        html.AppendLine("</script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static string BuildDataJson(IReadOnlyList<PriceCandle> candles, int timeframeMinutes)
    {
        var points = Aggregate(candles, timeframeMinutes);
        var sb = new StringBuilder("[");
        foreach (var p in points)
        {
            var unixSeconds = new DateTimeOffset(p.Time, TimeSpan.Zero).ToUnixTimeSeconds();
            sb.Append("{time:").Append(unixSeconds)
                .Append(",open:").Append(F(p.Open))
                .Append(",high:").Append(F(p.High))
                .Append(",low:").Append(F(p.Low))
                .Append(",close:").Append(F(p.Close))
                .Append("},");
        }

        if (points.Count > 0)
        {
            sb.Length -= 1;
        }

        return sb.Append("]").ToString();
    }

    private static List<CandlePoint> Aggregate(IReadOnlyList<PriceCandle> candles, int timeframeMinutes)
    {
        var span = TimeSpan.FromMinutes(timeframeMinutes).Ticks;

        var buckets = candles
            .GroupBy(c => c.Timestamp.Ticks / span)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(c => c.Timestamp).ToList())
            .ToList();

        var result = new List<CandlePoint>();
        foreach (var bucket in buckets)
        {
            var bucketStart = new DateTime(bucket[0].Timestamp.Ticks / span * span, DateTimeKind.Utc);
            result.Add(new CandlePoint
            {
                Time = bucketStart,
                Open = bucket[0].OpenPrice,
                High = bucket.Max(c => c.HighPrice),
                Low = bucket.Min(c => c.LowPrice),
                Close = bucket[^1].ClosePrice,
            });
        }

        return result;
    }

    private static string BuildCss()
    {
        var css = new StringBuilder();
        css.Append("body{margin:0;padding:24px;background:#14141c;color:#d5d5e0;font-family:'Segoe UI',Arial,sans-serif;}");
        css.Append(".card{max-width:1280px;margin:0 auto;background:#1c1c28;border:1px solid #2a2a3c;border-radius:14px;padding:22px 26px;}");
        css.Append("h1{margin:0 0 6px;font-size:21px;font-weight:600;color:#f0f0f7;}");
        css.Append(".sub{margin:0 0 16px;font-size:13px;color:#8f8fab;}");
        css.Append("#chart{width:1200px;height:600px;border:1px solid #2a2a3c;border-radius:8px;overflow:hidden;}");
        return css.ToString();
    }

    private static string F(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static void OpenInBrowser(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // Игнорируем, если браузер не удалось открыть
        }
    }

    private sealed class CandlePoint
    {
        public DateTime Time { get; init; }
        public decimal Open { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal Close { get; init; }
    }
}
