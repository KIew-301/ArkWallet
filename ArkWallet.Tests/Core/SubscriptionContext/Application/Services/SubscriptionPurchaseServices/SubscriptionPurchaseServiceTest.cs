using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Core.SubscriptionContext.Application.Events;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseServices;

public class SubscriptionPurchaseServiceTest
{
    private static SubscriptionPurchaseService CreateService(
        ArkWalletDbContext db,
        IPaymentIntegrationService payment,
        IEventPublisher eventPublisher,
        TimeProvider timeProvider) =>
        new(db, payment, eventPublisher, timeProvider, NullLogger<SubscriptionPurchaseService>.Instance);

    [Fact]
    public async Task PurchaseAsync_SubscriptionNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);

        var payment = new Mock<IPaymentIntegrationService>();
        var publisher = new Mock<IEventPublisher>();
        var service = CreateService(db, payment.Object, publisher.Object, new TestTimeProvider());

        var result = await service.PurchaseAsync(2000, 999);

        Assert.False(result.Success);
        payment.Verify(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_TraderNotFound_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        db.Subscriptions.Add(new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 });
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        var publisher = new Mock<IEventPublisher>();
        var service = CreateService(db, payment.Object, publisher.Object, new TestTimeProvider());

        var result = await service.PurchaseAsync(2000, 1);

        Assert.False(result.Success);
        payment.Verify(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_PaymentFails_ReturnsFailAndDoesNotUpdateTrader()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult { TransactionId = "TXN-FAIL", IsSuccess = false });
        var publisher = new Mock<IEventPublisher>();
        var service = CreateService(db, payment.Object, publisher.Object, new TestTimeProvider());

        var result = await service.PurchaseAsync(2000, sub.Id);

        Assert.False(result.Success);
        var trader = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        Assert.Null(trader.SubscriptionId);
        publisher.Verify(p => p.PublishAsync(It.IsAny<TraderSubscriptionChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseAsync_TimedSubscription_SetsExpiryAndHistoryAndPublishesEvent()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult { TransactionId = "TXN-100", IsSuccess = true });
        var publisher = new Mock<IEventPublisher>();
        var time = new TestTimeProvider();
        var service = CreateService(db, payment.Object, publisher.Object, time);

        var result = await service.PurchaseAsync(2000, sub.Id);

        Assert.True(result.Success);
        Assert.Equal("TXN-100", result.TransactionId);
        var expectedExpiry = time.GetUtcNow().UtcDateTime.AddMinutes(10080);
        Assert.Equal(expectedExpiry, result.ExpiresAtUtc);

        var trader = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        Assert.Equal(sub.Id, trader.SubscriptionId);
        Assert.Equal(expectedExpiry, trader.SubscriptionExpiresAtUtc);

        var history = await db.SubscriptionPurchaseHistory.CountAsync(h => h.TraderId == 2000);
        Assert.Equal(1, history);

        publisher.Verify(p => p.PublishAsync(It.IsAny<TraderSubscriptionChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_BasicInfiniteSubscription_ExpiryNull()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 2000);
        var sub = new Subscription { Name = "Базовая", Level = 1, PriceRubles = 0, MaxOrders = 5, MaxMiningMachines = 5, DurationMinutes = null };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult { TransactionId = "TXN-101", IsSuccess = true });
        var publisher = new Mock<IEventPublisher>();
        var service = CreateService(db, payment.Object, publisher.Object, new TestTimeProvider());

        var result = await service.PurchaseAsync(2000, sub.Id);

        Assert.True(result.Success);
        Assert.Null(result.ExpiresAtUtc);
        var trader = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == 2000);
        Assert.Equal(sub.Id, trader.SubscriptionId);
        Assert.Null(trader.SubscriptionExpiresAtUtc);
    }

    [Fact]
    public async Task PurchaseAsync_WhenPaymentThrows_ReturnsFail()
    {
        await using var db = await DbTest.CreateInitializedDbContextAsync();
        await HelpMethods.RegisterTrader(db, 3000);
        var sub = new Subscription { Name = "Премиум", Level = 2, PriceRubles = 100, MaxOrders = 20, MaxMiningMachines = 20, DurationMinutes = 10080 };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var payment = new Mock<IPaymentIntegrationService>();
        payment.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("payment error"));
        var publisher = new Mock<IEventPublisher>();
        var service = CreateService(db, payment.Object, publisher.Object, new TestTimeProvider());

        var result = await service.PurchaseAsync(3000, sub.Id);

        Assert.False(result.Success);
        Assert.Contains("payment error", result.Message);
    }
}
