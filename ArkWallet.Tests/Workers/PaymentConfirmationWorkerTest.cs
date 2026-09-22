using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Workers;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Workers;

public class PaymentConfirmationWorkerTest
{
    [Fact]
    public async Task ProcessPendingPayments_Succeeded_ActivatesAndMarksSucceeded()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "РџСЂРµРјРёСѓРј", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080, PriceMonthRubles = 290 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            Period = (int)SubscriptionPeriod.Month,
            AmountRubles = 290,
            ExternalPaymentId = "PAY-123",
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.GetPaymentStatusAsync("PAY-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult { PaymentId = "PAY-123", Status = "succeeded" });
        var activation = new Mock<ISubscriptionActivationService>();
        activation.Setup(a => a.ActivateAsync(2000, sub.Id, SubscriptionPeriod.Month, 290, "PAY-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionActivationResult(true, DateTime.UtcNow.AddDays(30)));

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton<ISubscriptionActivationService>(activation.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new PaymentConfirmationWorker(scopeFactory, NullLogger<PaymentConfirmationWorker>.Instance);
        await worker.ProcessPendingPaymentsAsync(CancellationToken.None);

        var updated = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("succeeded", updated.Status);
        Assert.NotNull(updated.SucceededAtUtc);
    }

    [Fact]
    public async Task ProcessPendingPayments_Canceled_MarksCanceled()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "РџСЂРµРјРёСѓРј", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080, PriceMonthRubles = 290 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            Period = (int)SubscriptionPeriod.Month,
            AmountRubles = 290,
            ExternalPaymentId = "PAY-999",
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.GetPaymentStatusAsync("PAY-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult { PaymentId = "PAY-999", Status = "canceled" });
        var activation = new Mock<ISubscriptionActivationService>();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton<ISubscriptionActivationService>(activation.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new PaymentConfirmationWorker(scopeFactory, NullLogger<PaymentConfirmationWorker>.Instance);
        await worker.ProcessPendingPaymentsAsync(CancellationToken.None);

        var updated = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("canceled", updated.Status);
        Assert.NotNull(updated.CanceledAtUtc);
        activation.Verify(a => a.ActivateAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PendingPaymentsExist_ProcessesAndCompletes()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "РџСЂРµРјРёСѓРј", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080, PriceMonthRubles = 290 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            Period = (int)SubscriptionPeriod.Month,
            AmountRubles = 290,
            ExternalPaymentId = "PAY-EXEC-1",
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.GetPaymentStatusAsync("PAY-EXEC-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult { PaymentId = "PAY-EXEC-1", Status = "succeeded" });
        var activation = new Mock<ISubscriptionActivationService>();
        activation.Setup(a => a.ActivateAsync(2000, sub.Id, SubscriptionPeriod.Month, 290, "PAY-EXEC-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionActivationResult(true, DateTime.UtcNow.AddDays(30)));

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton<ISubscriptionActivationService>(activation.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new PaymentConfirmationWorker(scopeFactory, NullLogger<PaymentConfirmationWorker>.Instance);

        await worker.ProcessPendingPaymentsAsync(CancellationToken.None);

        var updated = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("succeeded", updated.Status);
        Assert.NotNull(updated.SucceededAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_NonSuccessPaymentStatus_DoesNotActivate()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "РџСЂРµРјРёСѓРј", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080, PriceMonthRubles = 290 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            Period = (int)SubscriptionPeriod.Month,
            AmountRubles = 290,
            ExternalPaymentId = "PAY-NON-SUC",
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.GetPaymentStatusAsync("PAY-NON-SUC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult { PaymentId = "PAY-NON-SUC", Status = "pending" });
        var activation = new Mock<ISubscriptionActivationService>();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton<ISubscriptionActivationService>(activation.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new PaymentConfirmationWorker(scopeFactory, NullLogger<PaymentConfirmationWorker>.Instance);

        await worker.ProcessPendingPaymentsAsync(CancellationToken.None);

        var updated = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("pending", updated.Status);
        activation.Verify(a => a.ActivateAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PaymentProviderThrows_CaughtAndContinues()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "РџСЂРµРјРёСѓРј", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080, PriceMonthRubles = 290 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            TraderId = 2000,
            SubscriptionId = sub.Id,
            Period = (int)SubscriptionPeriod.Month,
            AmountRubles = 290,
            ExternalPaymentId = "PAY-THROW",
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.GetPaymentStatusAsync("PAY-THROW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused"));
        var activation = new Mock<ISubscriptionActivationService>();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton<ISubscriptionActivationService>(activation.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new PaymentConfirmationWorker(scopeFactory, NullLogger<PaymentConfirmationWorker>.Instance);

        await worker.ProcessPendingPaymentsAsync(CancellationToken.None);

        var updated = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("pending", updated.Status);
        activation.Verify(a => a.ActivateAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<SubscriptionPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
