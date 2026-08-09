using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class BotE2eAdminTests : IDisposable
{
    private readonly E2eHost _host = new();
    private readonly BotFlow _flow;

    public BotE2eAdminTests() => _flow = new BotFlow(_host);

    [Fact]
    public async Task AdminFlow_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.AdminAsync);

        await WarmupAsync();

        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        using (scope.Step("/admin_bots_activity"))
        {
            await _flow.WizardAsync(E2eConfig.MainAdminId, "/admin_bots_activity");
        }
        using (scope.Step("select_token"))
        {
            await _flow.WizardAsync(E2eConfig.MainAdminId, E2eConfig.Symbol);
        }
        using (scope.Step("/admin_stats"))
        {
            await _flow.WizardAsync(E2eConfig.MainAdminId, "/admin_stats");
        }
        using (scope.Step("/admin_get_ids"))
        {
            await _flow.WizardAsync(E2eConfig.MainAdminId, "/admin_get_ids");
        }

        GateAssert.QueryBudget("e2e-bot-admin-flow", GateBudgets.E2eBotAdminFlow, _host.QueryCounter, scope);
    }

    private async Task WarmupAsync()
    {
        await _flow.WizardAsync(E2eConfig.MainAdminId, "/admin_bots_activity");
        await _flow.WizardAsync(E2eConfig.MainAdminId, E2eConfig.Symbol);
        await _flow.WizardAsync(E2eConfig.MainAdminId, "/admin_stats");
        await _flow.WizardAsync(E2eConfig.MainAdminId, "/admin_get_ids");
    }

    public void Dispose() => _host.Dispose();
}
