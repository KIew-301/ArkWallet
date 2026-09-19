using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Infrastructure.Workers;
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
            .ReturnsAsync((DateTime?)null);

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        await task;

        expiry.Verify(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
            .Throws(new InvalidOperationException("boom"));
        expiry.Setup(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
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

        expiry.Verify(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
            .ReturnsAsync((DateTime?)null);

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        await task;

        expiry.Verify(x => x.ProcessExpiredAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        expiry.Verify(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
            .ReturnsAsync(DateTime.UtcNow.AddMinutes(30));

        var method = typeof(SubscriptionExpiryWorker).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(700));
        var task = (Task)method!.Invoke(worker, new object?[] { cts.Token })!;

        await task;

        expiry.Verify(x => x.GetNextExpiryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
