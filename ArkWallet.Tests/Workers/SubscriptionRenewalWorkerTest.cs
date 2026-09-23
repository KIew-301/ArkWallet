using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionActivationServices;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Workers;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Workers;

public class SubscriptionRenewalWorkerTest
{
    [Fact]
    public async Task ProcessRenewalsAsync_DueSubscriptionWithSavedMethod_CreatesRecurringPayment()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "Premium", Level = 2, PriceRubles = 100, PriceMonthRubles = 290, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 43200 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2000L);
        trader.SubscriptionId = sub.Id;
        trader.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddHours(5);
        trader.SavedPaymentMethodId = "pm-saved-1";
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRequest request, CancellationToken _) => new PaymentResult
            {
                IsSuccess = true,
                PaymentId = "PAY-RENEW-1",
                TransactionId = "PAY-RENEW-1",
                RequiresConfirmation = false,
            });

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new SubscriptionRenewalWorker(scopeFactory, NullLogger<SubscriptionRenewalWorker>.Instance);
        await worker.ProcessRenewalsAsync(CancellationToken.None);

        // Автопродление: создан рекуррентный платёж по сохранённому методу без пользователя.
        payment.Verify(p => p.CreatePaymentAsync(
            It.Is<PaymentRequest>(r => r.TraderTelegramId == 2000
                                     && r.PaymentMethodId == "pm-saved-1"
                                     && r.SubscriptionId == sub.Id),
            It.IsAny<CancellationToken>()), Times.Once);

        var record = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("pending", record.Status);
        Assert.Equal("PAY-RENEW-1", record.ExternalPaymentId);
        Assert.Equal("pm-saved-1", record.PaymentMethodId);
        Assert.Equal((int)SubscriptionPeriod.Month, record.Period);
    }

    [Fact]
    public async Task ProcessRenewalsAsync_WhenPaymentFails_NoRecordCreated()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2001);
        var sub = new Subscription { Name = "Premium", Level = 2, PriceRubles = 100, PriceMonthRubles = 290, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 43200 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2001L);
        trader.SubscriptionId = sub.Id;
        trader.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddHours(5);
        trader.SavedPaymentMethodId = "pm-saved-2";
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult { IsSuccess = false, PayerInfo = "yookassa_error" });

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();

        var worker = new SubscriptionRenewalWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SubscriptionRenewalWorker>.Instance);
        await worker.ProcessRenewalsAsync(CancellationToken.None);

        Assert.Empty(await db.SubscriptionPayments.ToListAsync());
    }

    [Fact]
    public async Task RenewalThenConfirmation_SubscriptionAutoRenews_ExpiryExtended()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2002);
        var sub = new Subscription { Name = "Premium", Level = 2, PriceRubles = 100, PriceMonthRubles = 290, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 43200 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var trader = await db.Traders.FirstAsync(t => t.TelegramId == 2002L);
        trader.SubscriptionId = sub.Id;
        trader.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddHours(5);
        trader.SavedPaymentMethodId = "pm-saved-3";
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRequest request, CancellationToken _) => new PaymentResult
            {
                IsSuccess = true,
                PaymentId = "PAY-RENEW-3",
                TransactionId = "PAY-RENEW-3",
                RequiresConfirmation = false,
            });
        payment.Setup(p => p.GetPaymentStatusAsync("PAY-RENEW-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult { PaymentId = "PAY-RENEW-3", Status = "succeeded", SavedPaymentMethodId = "pm-saved-3" });

        var mediator = new Mock<MediatR.IMediator>();
        var eventPublisher = new MediatREventPublisher(mediator.Object);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPaymentIntegrationService>(payment.Object);
        services.AddSingleton<ISubscriptionActivationService>(
            new SubscriptionActivationService(db, eventPublisher, TimeProvider.System, NullLogger<SubscriptionActivationService>.Instance));
        services.AddSingleton(TimeProvider.System);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var renewalWorker = new SubscriptionRenewalWorker(scopeFactory, NullLogger<SubscriptionRenewalWorker>.Instance);
        await renewalWorker.ProcessRenewalsAsync(CancellationToken.None);

        var record = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("pending", record.Status);

        // Подтверждение: автоплатёж успешен → подписка автоматически продлевается.
        var confirmationWorker = new PaymentConfirmationWorker(scopeFactory, NullLogger<PaymentConfirmationWorker>.Instance);
        await confirmationWorker.ProcessPendingPaymentsAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        var renewed = await db.Traders.FirstAsync(t => t.TelegramId == 2002L);
        Assert.NotNull(renewed.SubscriptionExpiresAtUtc);
        Assert.True(renewed.SubscriptionExpiresAtUtc > DateTime.UtcNow.AddDays(29), "Срок подписки должен быть продлён на месяц");

        var changed = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal("succeeded", changed.Status);
    }
}