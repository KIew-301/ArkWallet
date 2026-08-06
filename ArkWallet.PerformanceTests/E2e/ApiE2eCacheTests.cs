using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class ApiE2eCacheTests : IDisposable
{
    private readonly E2eHost _host = new(enableTokenCache: true);
    private readonly HttpClient _http;
    private readonly ApiFlow _flow;

    public ApiE2eCacheTests()
    {
        _http = _host.CreateClient();
        _flow = new ApiFlow(_http, _host.Configuration);
    }

    [Fact]
    public async Task RepeatedTokensCall_WithCache_HitsZeroQueriesOnSecondRun()
    {
        await _host.SeedAsync(E2eSeed.DashboardAsync);

        await WarmupAsync();

        CachingTokenQueryService.Clear(_host.Services.GetRequiredService<IMemoryCache>());

        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        string token;
        using (scope.Step("login"))
        {
            token = await _flow.LoginAsync(E2eConfig.TraderId);
        }
        _flow.Authorize(token);

        using (scope.Step("tokens-first"))
        {
            await GetOkAsync("/api/v1/tokens/token");
        }
        using (scope.Step("tokens-repeat"))
        {
            await GetOkAsync("/api/v1/tokens/token");
        }

        var report = scope.Report();
        GateAssert.QueryBudget("e2e-cache-check", GateBudgets.E2eCacheCheck, _host.QueryCounter, scope);

        Assert.True(report.Steps.Single(s => s.Name == "tokens-first").Queries > 0,
            "Первый прогон не сделал запросов — кеш не проверен");
        Assert.Equal(0, report.Steps.Single(s => s.Name == "tokens-repeat").Queries);
    }

    private async Task WarmupAsync()
    {
        var token = await _flow.LoginAsync(E2eConfig.TraderId);
        _flow.Authorize(token);
        await GetOkAsync("/api/v1/tokens/token");
    }

    private async Task GetOkAsync(string url)
    {
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _host.Dispose();
}
