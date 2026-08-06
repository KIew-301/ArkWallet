using System.Net.Http.Json;
using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class ApiE2eTradingTests : IDisposable
{
    private readonly E2eHost _host = new();
    private readonly HttpClient _http;
    private readonly ApiFlow _flow;

    public ApiE2eTradingTests()
    {
        _http = _host.CreateClient();
        _flow = new ApiFlow(_http, _host.Configuration);
    }

    [Fact]
    public async Task TradingFlow_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.TradingAsync);

        await WarmupAsync();

        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        string token;
        using (scope.Step("login"))
        {
            token = await _flow.LoginAsync(E2eConfig.TraderId);
        }
        _flow.Authorize(token);

        string orderId;
        using (scope.Step("create-buy"))
        {
            orderId = await CreateBuyOrderAsync();
        }
        using (scope.Step("orders"))
        {
            await GetOkAsync("/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true");
        }
        using (scope.Step("trades"))
        {
            await GetOkAsync("/api/v1/trades/trade");
        }
        using (scope.Step("cancel"))
        {
            await DeleteOrderAsync(orderId);
        }

        GateAssert.QueryBudget("e2e-trading-flow", GateBudgets.E2eTradingFlow, _host.QueryCounter, scope);
    }

    private async Task WarmupAsync()
    {
        var token = await _flow.LoginAsync(E2eConfig.TraderId);
        _flow.Authorize(token);
        var orderId = await CreateBuyOrderAsync();
        await GetOkAsync("/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true");
        await GetOkAsync("/api/v1/trades/trade");
        await DeleteOrderAsync(orderId);
    }

    private async Task<string> CreateBuyOrderAsync()
    {
        using var response = await _http.PostAsJsonAsync("/api/v1/orders/order", new
        {
            symbol = E2eConfig.Symbol,
            price = E2eConfig.Price,
            quantity = E2eConfig.Quantity,
            direction = E2eConfig.DirectionBuy
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreateOrderBody>();
        return body!.OrderId;
    }

    private async Task GetOkAsync(string url)
    {
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteOrderAsync(string orderId)
    {
        using var response = await _http.DeleteAsync($"/api/v1/orders/order/{orderId}");
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _host.Dispose();

    private sealed record CreateOrderBody(string OrderId, bool IsFilled);
}
