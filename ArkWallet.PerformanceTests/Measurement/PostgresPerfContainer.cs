using Testcontainers.PostgreSql;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class PostgresPerfContainer
{
    private static readonly Lazy<Task<PostgreSqlContainer>> LazyContainer = new(async () =>
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("arkwallet_perf")
            .WithUsername("arkwallet")
            .WithPassword("arkwallet")
            .Build();

        await container.StartAsync();
        return container;
    });

    private static bool _cleanupRegistered;

    public static async Task<string> GetConnectionStringAsync()
    {
        EnsureCleanupRegistered();
        var container = await LazyContainer.Value;
        return container.GetConnectionString();
    }

    private static void EnsureCleanupRegistered()
    {
        if (_cleanupRegistered)
            return;

        _cleanupRegistered = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (LazyContainer.IsValueCreated)
                LazyContainer.Value.GetAwaiter().GetResult().DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
    }
}
