using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArkWallet.PerformanceTests.E2e;

internal sealed class E2eHost : WebApplicationFactory<Program>
{
    private readonly bool _enableTokenCache;
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    internal E2eHost(bool enableTokenCache = false)
    {
        _enableTokenCache = enableTokenCache;
        _connection.Open();
    }

    internal QueryCounter QueryCounter { get; } = new();
    internal SaveChangesCounter SaveChangesCounter { get; } = new();

    internal IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<RabbitMQService>();
            services.RemoveAll<ITaskDispatcher>();
            services.AddScoped<ITaskDispatcher, FakeTaskDispatcher>();

            services.RemoveAll<DbContextOptions<ArkWalletDbContext>>();
            services.RemoveAll<ArkWalletDbContext>();
            services.AddDbContext<ArkWalletDbContext>(options => options
                .UseSqlite(_connection)
                .AddInterceptors(QueryCounter, SaveChangesCounter));

            if (_enableTokenCache)
            {
                services.AddMemoryCache();
                services.RemoveAll<ITokenQueryService>();
                services.AddScoped<TokenQueryService>();
                services.AddScoped<ITokenQueryService>(sp => new CachingTokenQueryService(
                    sp.GetRequiredService<TokenQueryService>(),
                    sp.GetRequiredService<IMemoryCache>()));
            }
        });
    }

    internal void ResetCounters()
    {
        QueryCounter.Reset();
        SaveChangesCounter.Reset();
    }

    internal async Task SeedAsync(Func<ArkWalletDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
        await db.Database.EnsureCreatedAsync();
        await action(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
