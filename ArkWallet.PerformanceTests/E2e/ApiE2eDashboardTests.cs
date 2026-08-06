using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class ApiE2eDashboardTests : IDisposable
{
    private readonly E2eHost _host = new();
    private readonly HttpClient _http;
    private readonly ApiFlow _flow;

    public ApiE2eDashboardTests()
    {
        _http = _host.CreateClient();
        _flow = new ApiFlow(_http, _host.Configuration);
    }

    [Fact]
    public async Task DashboardFlow_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.DashboardAsync);

        await WarmupAsync();

        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        string token;
        using (scope.Step("login"))
        {
            token = await _flow.LoginAsync(E2eConfig.TraderId);
        }
        _flow.Authorize(token);

        using (scope.Step("tokens"))
        {
            await GetOkAsync("/api/v1/tokens/token");
        }
        using (scope.Step("candles"))
        {
            await GetOkAsync(CandleUrl());
        }
        using (scope.Step("orders"))
        {
            await GetOkAsync("/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true");
        }
        using (scope.Step("portfolios"))
        {
            await GetOkAsync("/api/v1/portfolios/portfolio");
        }
        using (scope.Step("balance"))
        {
            await GetOkAsync("/api/v1/traders/balance?periodDays=1");
        }

        GateAssert.QueryBudget("e2e-dashboard-flow", GateBudgets.E2eDashboardFlow, _host.QueryCounter, scope);
    }

    private async Task WarmupAsync()
    {
        var token = await _flow.LoginAsync(E2eConfig.TraderId);
        _flow.Authorize(token);
        await GetOkAsync("/api/v1/tokens/token");
        await GetOkAsync(CandleUrl());
        await GetOkAsync("/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true");
        await GetOkAsync("/api/v1/portfolios/portfolio");
        await GetOkAsync("/api/v1/traders/balance?periodDays=1");
    }

    private static string CandleUrl()
    {
        var start = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var end = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        return $"/api/v1/tokens/candle?symbol={E2eConfig.Symbol}&startDateTimeOffset={start}&endDateTimeOffset={end}&timeFrameInMinutes=1";
    }

    private async Task GetOkAsync(string url)
    {
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _host.Dispose();
}
