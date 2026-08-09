using System.Net.Http.Json;
using ArkWallet.PerformanceTests.E2e;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ArkWallet.PerformanceTests.Repeats;

[Collection("Perf")]
public sealed class E2eRepeatTests
{
    [Fact]
    public async Task DashboardFlow_Repeat()
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        using var host = new E2eHost();
        var http = host.CreateClient();
        var flow = new ApiFlow(http, host.Configuration);
        await host.SeedAsync(E2eSeed.DashboardAsync);

        await DashboardOnceAsync(host, http, flow);

        var report = await RepeatRun.RunAsync("e2e-dashboard-flow", repeats, () => DashboardOnceAsync(host, http, flow));
        ReportSession.RecordRepeat(report);
    }

    [Fact]
    public async Task TradingFlow_Repeat()
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        using var host = new E2eHost();
        var http = host.CreateClient();
        var flow = new ApiFlow(http, host.Configuration);
        await host.SeedAsync(E2eSeed.TradingAsync);

        await TradingOnceAsync(host, http, flow);

        var report = await RepeatRun.RunAsync("e2e-trading-flow", repeats, () => TradingOnceAsync(host, http, flow));
        ReportSession.RecordRepeat(report);
    }

    [Fact]
    public async Task CacheCheck_Repeat()
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        using var host = new E2eHost(enableTokenCache: true);
        var http = host.CreateClient();
        var flow = new ApiFlow(http, host.Configuration);
        await host.SeedAsync(E2eSeed.DashboardAsync);

        await CacheOnceAsync(host, http, flow);

        var report = await RepeatRun.RunAsync("e2e-cache-check", repeats, () => CacheOnceAsync(host, http, flow));
        ReportSession.RecordRepeat(report);
    }

    [Fact]
    public async Task WizardFlow_Repeat()
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        using var host = new E2eHost();
        var flow = new BotFlow(host);
        await host.SeedAsync(E2eSeed.WizardAsync);

        await WizardOnceAsync(host, flow);

        var report = await RepeatRun.RunAsync("e2e-bot-wizard-flow", repeats, () => WizardOnceAsync(host, flow));
        ReportSession.RecordRepeat(report);
    }

    [Fact]
    public async Task AdminFlow_Repeat()
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        using var host = new E2eHost();
        var flow = new BotFlow(host);
        await host.SeedAsync(E2eSeed.AdminAsync);

        await AdminOnceAsync(host, flow);

        var report = await RepeatRun.RunAsync("e2e-bot-admin-flow", repeats, () => AdminOnceAsync(host, flow));
        ReportSession.RecordRepeat(report);
    }

    [Fact]
    public async Task TelegramBotLevel_Repeat()
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        using var host = new E2eHost();
        var flow = new BotFlow(host);
        await host.SeedAsync(E2eSeed.TelegramLevelAsync);

        await TelegramOnceAsync(host, flow);

        var report = await RepeatRun.RunAsync("e2e-telegram-bot-level", repeats, () => TelegramOnceAsync(host, flow));
        ReportSession.RecordRepeat(report);
    }

    private static async Task<PerfReport> DashboardOnceAsync(E2eHost host, HttpClient http, ApiFlow flow)
    {
        host.ResetCounters();
        using var scope = new PerfScope(host.QueryCounter);

        string token;
        using (scope.Step("login"))
        {
            token = await flow.LoginAsync(E2eConfig.TraderId);
        }
        flow.Authorize(token);

        using (scope.Step("tokens"))
        {
            await GetOkAsync(http, "/api/v1/tokens/token");
        }
        using (scope.Step("candles"))
        {
            await GetOkAsync(http, CandleUrl());
        }
        using (scope.Step("orders"))
        {
            await GetOkAsync(http, "/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true");
        }
        using (scope.Step("portfolios"))
        {
            await GetOkAsync(http, "/api/v1/portfolios/portfolio");
        }
        using (scope.Step("balance"))
        {
            await GetOkAsync(http, "/api/v1/traders/balance?periodDays=1");
        }

        return scope.Report();
    }

    private static async Task<PerfReport> TradingOnceAsync(E2eHost host, HttpClient http, ApiFlow flow)
    {
        host.ResetCounters();
        using var scope = new PerfScope(host.QueryCounter);

        string token;
        using (scope.Step("login"))
        {
            token = await flow.LoginAsync(E2eConfig.TraderId);
        }
        flow.Authorize(token);

        string orderId;
        using (scope.Step("create-buy"))
        {
            orderId = await CreateBuyOrderAsync(http);
        }
        using (scope.Step("orders"))
        {
            await GetOkAsync(http, "/api/v1/orders/order?includeActive=true&includeFilled=true&includeCancelled=true");
        }
        using (scope.Step("trades"))
        {
            await GetOkAsync(http, "/api/v1/trades/trade");
        }
        using (scope.Step("cancel"))
        {
            await DeleteOrderAsync(http, orderId);
        }

        return scope.Report();
    }

    private static async Task<PerfReport> CacheOnceAsync(E2eHost host, HttpClient http, ApiFlow flow)
    {
        CachingTokenQueryService.Clear(host.Services.GetRequiredService<IMemoryCache>());
        CacheCounters.Reset();
        host.ResetCounters();
        using var scope = new PerfScope(host.QueryCounter);

        string token;
        using (scope.Step("login"))
        {
            token = await flow.LoginAsync(E2eConfig.TraderId);
        }
        flow.Authorize(token);

        using (scope.Step("tokens-first"))
        {
            await GetOkAsync(http, "/api/v1/tokens/token");
        }
        using (scope.Step("tokens-repeat"))
        {
            await GetOkAsync(http, "/api/v1/tokens/token");
        }

        var report = scope.Report();
        return report with
        {
            Counters = new[]
            {
                new CounterRecord("cache-hits", CacheCounters.Hits),
                new CounterRecord("cache-misses", CacheCounters.Misses)
            }
        };
    }

    private static async Task<PerfReport> WizardOnceAsync(E2eHost host, BotFlow flow)
    {
        host.ResetCounters();
        using var scope = new PerfScope(host.QueryCounter);
        await RunWizardAsync(flow, scope);
        return scope.Report();
    }

    private static async Task<PerfReport> AdminOnceAsync(E2eHost host, BotFlow flow)
    {
        host.ResetCounters();
        using var scope = new PerfScope(host.QueryCounter);
        using (scope.Step("/admin_bots_activity"))
        {
            await flow.WizardAsync(E2eConfig.MainAdminId, "/admin_bots_activity");
        }
        using (scope.Step("select_token"))
        {
            await flow.WizardAsync(E2eConfig.MainAdminId, E2eConfig.Symbol);
        }
        using (scope.Step("/admin_stats"))
        {
            await flow.WizardAsync(E2eConfig.MainAdminId, "/admin_stats");
        }
        using (scope.Step("/admin_get_ids"))
        {
            await flow.WizardAsync(E2eConfig.MainAdminId, "/admin_get_ids");
        }

        return scope.Report();
    }

    private static async Task<PerfReport> TelegramOnceAsync(E2eHost host, BotFlow flow)
    {
        host.ResetCounters();
        using var scope = new PerfScope(host.QueryCounter);
        using (scope.Step("/get_tops"))
        {
            await flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_tops 10");
        }
        using (scope.Step("/get_orders"))
        {
            await flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_orders");
        }
        using (scope.Step("/admin_stats"))
        {
            await flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/admin_stats");
        }
        using (scope.Step("/get_profile"))
        {
            await flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_profile");
        }

        return scope.Report();
    }

    private static async Task RunWizardAsync(BotFlow flow, PerfScope scope)
    {
        using (scope.Step("/start"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, "/start");
        }
        using (scope.Step("/place_order"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, "/place_order");
        }
        using (scope.Step("set_direction"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, E2eConfig.DirectionBuy);
        }
        using (scope.Step("set_token"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Symbol);
        }
        using (scope.Step("set_quantity"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Quantity.ToString());
        }
        using (scope.Step("set_price"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Price.ToString("0.##"));
        }
        using (scope.Step("/get_orders"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, "/get_orders");
        }
        using (scope.Step("/get_profile"))
        {
            await flow.WizardAsync(E2eConfig.TraderId, "/get_profile");
        }
    }

    private static string CandleUrl()
    {
        var start = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var end = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        return $"/api/v1/tokens/candle?symbol={E2eConfig.Symbol}&startDateTimeOffset={start}&endDateTimeOffset={end}&timeFrameInMinutes=1";
    }

    private static async Task<string> CreateBuyOrderAsync(HttpClient http)
    {
        using var response = await http.PostAsJsonAsync("/api/v1/orders/order", new
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

    private static async Task GetOkAsync(HttpClient http, string url)
    {
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private static async Task DeleteOrderAsync(HttpClient http, string orderId)
    {
        using var response = await http.DeleteAsync($"/api/v1/orders/order/{orderId}");
        response.EnsureSuccessStatusCode();
    }

    private sealed record CreateOrderBody(string OrderId, bool IsFilled);
}
