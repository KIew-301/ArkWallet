using System.Net.Http.Json;
using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Xunit.Abstractions;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class ApiE2eHeavyGateTests : IDisposable
{
    private const decimal CreatePrice = 1500m;
    private const int CreateQuantity = 10;
    private const int CandlePeriodDays = 100;
    private const int CandleTimeframeMinutes = 60;

    private readonly E2eHost _host = new();
    private readonly HttpClient _http;
    private readonly ApiFlow _flow;
    private readonly ITestOutputHelper _output;

    public ApiE2eHeavyGateTests(ITestOutputHelper output)
    {
        _output = output;
        _http = _host.CreateClient();
        _flow = new ApiFlow(_http, _host.Configuration);
    }

    [Fact]
    public async Task GetOrders_With10kOrders_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyOrdersGetAsync);
        await WarmupAsync(OrdersUrl);

        await MeasureAsync(
            "heavy-orders-get",
            GateBudgets.HeavyOrdersGet,
            async scope =>
            {
                using (scope.Step("GET /orders"))
                {
                    await GetOkAsync(OrdersUrl);
                }
            });
    }

    [Fact]
    public async Task CreateOrder_With2kAskBook_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyOrderCreateAsync);
        await WarmupAsync("/api/v1/tokens/token");

        await MeasureAsync(
            "heavy-order-create",
            GateBudgets.HeavyOrderCreate,
            async scope =>
            {
                using (scope.Step("POST /orders"))
                {
                    using var response = await _http.PostAsJsonAsync("/api/v1/orders/order", new
                    {
                        symbol = E2eConfig.Symbol,
                        price = CreatePrice,
                        quantity = CreateQuantity,
                        direction = E2eConfig.DirectionBuy
                    });
                    response.EnsureSuccessStatusCode();
                }
            },
            saveChanges: true);
    }

    [Fact]
    public async Task CancelAllOrders_With2kActive_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyOrdersCancelAllAsync);
        await WarmupAsync("/api/v1/tokens/token");

        await MeasureAsync(
            "heavy-orders-cancel-all",
            GateBudgets.HeavyOrdersCancelAll,
            async scope =>
            {
                using (scope.Step("DELETE /orders"))
                {
                    using var response = await _http.DeleteAsync("/api/v1/orders/orders");
                    response.EnsureSuccessStatusCode();
                }
            },
            saveChanges: true);
    }

    [Fact]
    public async Task GetToken_With500Tokens_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyTokensGetAsync);
        await WarmupAsync("/api/v1/tokens/token");

        await MeasureAsync(
            "heavy-tokens-get",
            GateBudgets.HeavyTokensGet,
            async scope =>
            {
                using (scope.Step("GET /tokens"))
                {
                    await GetOkAsync("/api/v1/tokens/token");
                }
            });
    }

    [Fact]
    public async Task GetPriceCandle_With100kCandles_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyCandleGetAsync);
        await WarmupAsync(CandleUrl);

        await MeasureAsync(
            "heavy-candle-get",
            GateBudgets.HeavyCandleGet,
            async scope =>
            {
                using (scope.Step("GET /candle"))
                {
                    await GetOkAsync(CandleUrl);
                }
            });
    }

    [Fact]
    public async Task GetBalance_With10kSnapshots_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyBalanceGetAsync);
        await WarmupAsync("/api/v1/traders/balance?periodDays=30");

        await MeasureAsync(
            "heavy-balance-get",
            GateBudgets.HeavyBalanceGet,
            async scope =>
            {
                using (scope.Step("GET /balance"))
                {
                    await GetOkAsync("/api/v1/traders/balance?periodDays=30");
                }
            });
    }

    [Fact]
    public async Task GetTrades_With20kTrades_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.HeavyTradesGetAsync);
        await WarmupAsync("/api/v1/trades/trade");

        await MeasureAsync(
            "heavy-trades-get",
            GateBudgets.HeavyTradesGet,
            async scope =>
            {
                using (scope.Step("GET /trades"))
                {
                    await GetOkAsync("/api/v1/trades/trade");
                }
            });
    }

    private async Task MeasureAsync(
        string scenario,
        Budget budget,
        Func<PerfScope, Task> measured,
        bool saveChanges = false)
    {
        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        await measured(scope);

        Dump(scenario, scope, saveChanges ? _host.SaveChangesCounter : null);

        GateAssert.QueryBudget(scenario, budget, _host.QueryCounter, scope,
            saveChanges ? _host.SaveChangesCounter : null);
    }

    private async Task WarmupAsync(string url)
    {
        var token = await _flow.LoginAsync(E2eConfig.TraderId);
        _flow.Authorize(token);
        await GetOkAsync(url);
    }

    private string CandleUrl
    {
        get
        {
            var start = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-CandlePeriodDays).ToString("O"));
            var end = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
            return $"/api/v1/tokens/candle?symbol={E2eConfig.Symbol}&startDateTimeOffset={start}&endDateTimeOffset={end}&timeFrameInMinutes={CandleTimeframeMinutes}";
        }
    }

    private const string OrdersUrl = "/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true";

    private void Dump(string scenario, PerfScope scope, SaveChangesCounter? save)
    {
        var report = scope.Report();
        var lines = new List<string>
        {
            $"[{scenario}] totalMs={report.TotalMs:0.##} queries={report.TotalQueries} rows={report.TotalRows}" +
            (save != null ? $" saveChanges={save.Count}" : string.Empty)
        };

        foreach (var step in report.Steps)
            lines.Add($"  step {step.Name}: {step.Ms:0.##} ms, {step.Queries} q, {step.Rows} r");

        _output.WriteLine(string.Join(Environment.NewLine, lines));
    }

    private async Task GetOkAsync(string url)
    {
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _host.Dispose();
}
