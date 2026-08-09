using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.Repeats;

[Collection("Perf")]
public sealed class ServiceRepeatTests
{
    [Fact]
    public async Task TokenQuery_Repeat()
        => await RunService("token-query-50t", counter => ScenarioBodies.TokenQueryAsync(counter));

    [Fact]
    public async Task BalanceMain_Repeat()
        => await RunService("balance-main-changes", counter => ScenarioBodies.BalanceMainAsync(counter));

    [Fact]
    public async Task BalanceTotal_Repeat()
        => await RunService("balance-total-changes", counter => ScenarioBodies.BalanceTotalAsync(counter));

    [Fact]
    public async Task LeadersTop_Repeat()
        => await RunService("leaders-top-50t", counter => ScenarioBodies.LeadersTopAsync(counter));

    [Fact]
    public async Task OrderCreateBuy_Repeat()
        => await RunService("order-create-buy", counter => ScenarioBodies.OrderCreateAsync(counter, "купить"));

    [Fact]
    public async Task OrderCreateSell_Repeat()
        => await RunService("order-create-sell", counter => ScenarioBodies.OrderCreateAsync(counter, "продать"));

    [Theory]
    [InlineData(10, "market-maker-tick-10t")]
    [InlineData(20, "market-maker-tick-20t")]
    public async Task MmTick_Repeat(int tokenCount, string scenario)
        => await RunService(scenario, counter => ScenarioBodies.MmTickAsync(counter, tokenCount));

    private static async Task RunService(string scenario, Func<QueryCounter, Task<PerfReport>> body)
    {
        var repeats = RepeatConfig.Repeats;
        if (repeats <= 0)
            return;

        var counter = new QueryCounter();
        await body(counter);

        var report = await RepeatRun.RunAsync(scenario, repeats, () => body(counter));
        ReportSession.RecordRepeat(report);
    }
}
