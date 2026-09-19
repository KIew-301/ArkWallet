using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Infrastructure.Payment;
using Xunit;

namespace ArkWallet.Tests.InfrastructureTests;

public class InstantSuccessPaymentServiceTest
{
    [Fact]
    public async Task CreatePaymentAsync_ReturnsSucceededResultWithTransactionId()
    {
        var service = new InstantSuccessPaymentService();
        var result = await service.CreatePaymentAsync(new PaymentRequest
        {
            TraderTelegramId = 1,
            AmountRubles = 100,
            SubscriptionId = 1,
            Description = "test"
        });

        Assert.IsType<PaymentResult>(result);
        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.TransactionId));
        Assert.Null(result.PayerInfo);
    }

    [Fact]
    public async Task CreatePaymentAsync_ReturnsDistinctTransactionIds()
    {
        var service = new InstantSuccessPaymentService();
        var result1 = await service.CreatePaymentAsync(new PaymentRequest
        {
            TraderTelegramId = 1,
            AmountRubles = 100,
            SubscriptionId = 1
        });

        var result2 = await service.CreatePaymentAsync(new PaymentRequest
        {
            TraderTelegramId = 1,
            AmountRubles = 100,
            SubscriptionId = 1
        });

        Assert.NotEqual(result1.TransactionId, result2.TransactionId);
    }
}
