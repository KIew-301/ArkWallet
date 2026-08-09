using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class BotE2eTelegramTests : IDisposable
{
    private readonly E2eHost _host = new();
    private readonly BotFlow _flow;

    public BotE2eTelegramTests() => _flow = new BotFlow(_host);

    [Fact]
    public async Task TelegramBotLevel_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.TelegramLevelAsync);

        await WarmupAsync();

        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        using (scope.Step("/get_tops"))
        {
            await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_tops 10");
        }
        using (scope.Step("/get_orders"))
        {
            await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_orders");
        }
        using (scope.Step("/admin_stats"))
        {
            await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/admin_stats");
        }
        using (scope.Step("/get_profile"))
        {
            await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_profile");
        }

        GateAssert.QueryBudget("e2e-telegram-bot-level", GateBudgets.E2eTelegramBotLevel, _host.QueryCounter, scope);
    }

    private async Task WarmupAsync()
    {
        await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_tops 10");
        await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_orders");
        await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/admin_stats");
        await _flow.RunTelegramLevelAsync(E2eConfig.MainAdminId, "/get_profile");
    }

    public void Dispose() => _host.Dispose();
}
