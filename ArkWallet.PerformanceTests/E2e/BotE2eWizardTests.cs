using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.E2e;

[Collection("Perf")]
public sealed class BotE2eWizardTests : IDisposable
{
    private readonly E2eHost _host = new();
    private readonly BotFlow _flow;

    public BotE2eWizardTests() => _flow = new BotFlow(_host);

    [Fact]
    public async Task WizardFlow_StaysWithinBudget()
    {
        await _host.SeedAsync(E2eSeed.WizardAsync);

        await WarmupAsync();

        _host.ResetCounters();
        using var scope = new PerfScope(_host.QueryCounter);

        using (scope.Step("/start"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, "/start");
        }
        using (scope.Step("/place_order"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, "/place_order");
        }
        using (scope.Step("set_direction"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.DirectionBuy);
        }
        using (scope.Step("set_token"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Symbol);
        }
        using (scope.Step("set_quantity"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Quantity.ToString());
        }
        using (scope.Step("set_price"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Price.ToString("0.##"));
        }
        using (scope.Step("/get_orders"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, "/get_orders");
        }
        using (scope.Step("/get_profile"))
        {
            await _flow.WizardAsync(E2eConfig.TraderId, "/get_profile");
        }

        GateAssert.QueryBudget("e2e-bot-wizard-flow", GateBudgets.E2eBotWizardFlow, _host.QueryCounter, scope);
    }

    private async Task WarmupAsync()
    {
        await _flow.WizardAsync(E2eConfig.TraderId, "/start");
        await _flow.WizardAsync(E2eConfig.TraderId, "/place_order");
        await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.DirectionBuy);
        await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Symbol);
        await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Quantity.ToString());
        await _flow.WizardAsync(E2eConfig.TraderId, E2eConfig.Price.ToString("0.##"));
        await _flow.WizardAsync(E2eConfig.TraderId, "/get_orders");
        await _flow.WizardAsync(E2eConfig.TraderId, "/get_profile");
    }

    public void Dispose() => _host.Dispose();
}
