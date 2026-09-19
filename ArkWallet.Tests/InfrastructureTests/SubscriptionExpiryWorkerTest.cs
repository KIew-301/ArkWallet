using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Workers;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.InfrastructureTests;

public class SubscriptionExpiryWorkerTest
{
    private static (Mock<IServiceScopeFactory> ScopeFactory, Mock<IServiceScope> Scope, Mock<IServiceProvider> Provider, Mock<ISubscriptionExpiryService> Expiry) BuildMocks()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var provider = new Mock<IServiceProvider>();
        var expiry = new Mock<ISubscriptionExpiryService>();

        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
        provider.Setup(p => p.GetService(typeof(ISubscriptionExpiryService))).Returns(expiry.Object);

        return (scopeFactory, scope, provider, expiry);
    }

    [Fact]
    public async Task Handle_CompletesAndReturnsCompletedTask()
    {
        var (scopeFactory, _, _, _) = BuildMocks();
        var worker = new SubscriptionExpiryWorker(scopeFactory.Object, NullLogger<SubscriptionExpiryWorker>.Instance);

        var evt = new TraderSubscriptionChangedEvent(2000);
        var result = worker.Handle(evt, CancellationToken.None);

        Assert.Same(Task.CompletedTask, result);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesSubscriptionAndStopsOnCancellation()
    {
        var (scopeFactory, _, _, expiry) = BuildMocks();
        var logger = NullLogger<SubscriptionExpiryWorker>.Instance;
        var worker = new SubscriptionExpiryWorker(scopeFactory.Object, logger);

        expiry.Setup(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        expiry.Setup(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TraderSubscriptionExpiry(2000, DateTime.UtcNow.AddSeconds(-1)));
        expiry.Setup(x => x.DowngradeToBasicAsync(2000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1800));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        await task;

        expiry.Verify(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()), Times.Once);
        expiry.Verify(x => x.DowngradeToBasicAsync(2000, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        expiry.Verify(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        scopeFactory.Verify(f => f.CreateScope(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceThrows_LogsErrorThenStopsOnCancellation()
    {
        var (scopeFactory, _, _, expiry) = BuildMocks();
        var logger = NullLogger<SubscriptionExpiryWorker>.Instance;
        var worker = new SubscriptionExpiryWorker(scopeFactory.Object, logger);

        expiry.Setup(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        expiry.Setup(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TraderSubscriptionExpiry(2000, DateTime.UtcNow.AddSeconds(-1)));
        expiry.Setup(x => x.DowngradeToBasicAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1800));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
        }

        expiry.Verify(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()), Times.Once);
        expiry.Verify(x => x.DowngradeToBasicAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        scopeFactory.Verify(f => f.CreateScope(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_NoExpiryWaitsHourAndStopsOnCancellation()
    {
        var (scopeFactory, _, _, expiry) = BuildMocks();
        var logger = NullLogger<SubscriptionExpiryWorker>.Instance;
        var worker = new SubscriptionExpiryWorker(scopeFactory.Object, logger);

        expiry.Setup(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        expiry.Setup(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((TraderSubscriptionExpiry?)null);

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        await task;

        expiry.Verify(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()), Times.Once);
        expiry.Verify(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        expiry.Verify(x => x.DowngradeToBasicAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        scopeFactory.Verify(f => f.CreateScope(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ComputesDelayFromNextExpiryAndStopsOnCancellation()
    {
        var (scopeFactory, _, _, expiry) = BuildMocks();
        var logger = NullLogger<SubscriptionExpiryWorker>.Instance;
        var worker = new SubscriptionExpiryWorker(scopeFactory.Object, logger);

        expiry.Setup(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        expiry.Setup(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TraderSubscriptionExpiry(2000, DateTime.UtcNow.AddMinutes(30)));

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        await task;

        expiry.Verify(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithRealService_DowngradesOnlyExpiredTraders()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        var basic = new Subscription { Name = "Basic", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        var premium = new Subscription { Name = "Premium", Level = 2, PriceRubles = 500, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 100 };
        db.Subscriptions.Add(basic); db.Subscriptions.Add(premium);
        await db.SaveChangesAsync();

        await HelpMethods.RegisterTrader(db, 6001);
        await HelpMethods.RegisterTrader(db, 6002);
        var near = await db.Traders.FirstAsync(t => t.TelegramId == 6001L);
        near.SubscriptionId = premium.Id;
        near.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddSeconds(1.5);
        var far = await db.Traders.FirstAsync(t => t.TelegramId == 6002L);
        far.SubscriptionId = premium.Id;
        far.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddYears(5);
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton<ArkWalletDbContext>(db);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<SubscriptionExpiryService>>(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SubscriptionExpiryService>.Instance);
        services.AddScoped<ISubscriptionExpiryService, SubscriptionExpiryService>();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new SubscriptionExpiryWorker(scopeFactory, NullLogger<SubscriptionExpiryWorker>.Instance);
        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
        await (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        db.ChangeTracker.Clear();
        var updatedNear = await db.Traders.FirstAsync(t => t.TelegramId == 6001L);
        var updatedFar = await db.Traders.FirstAsync(t => t.TelegramId == 6002L);
        Assert.Equal(basic.Id, updatedNear.SubscriptionId);
        Assert.Null(updatedNear.SubscriptionExpiresAtUtc);
        Assert.Equal(premium.Id, updatedFar.SubscriptionId);
        Assert.NotNull(updatedFar.SubscriptionExpiresAtUtc);
    }
}
