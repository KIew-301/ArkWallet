using ArkWallet.Infrastructure.Data;
using ArkWallet.Presentation.Health;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArkWallet.Tests.HealthTests;

public class DatabaseHealthCheckTest
{
    [Fact]
    public async Task CheckHealthAsync_WhenDbAvailable_ReturnsHealthy()
    {
        var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var check = new DatabaseHealthCheck(new FakeScopeFactory(db));
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDbNotResolvable_ReturnsUnhealthy()
    {
        var check = new DatabaseHealthCheck(new FakeScopeFactory(null));
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private sealed class FakeScopeFactory(ArkWalletDbContext? db) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(db);
    }

    private sealed class FakeScope(ArkWalletDbContext? db) : IServiceScope
    {
        public IServiceProvider ServiceProvider => new FakeProvider(db);
        public void Dispose() { }
    }

    private sealed class FakeProvider(ArkWalletDbContext? db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ArkWalletDbContext) ? db : null;
    }
}
