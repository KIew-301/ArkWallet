using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Infrastructure.Payment;

public class InstantSuccessPaymentService : IPaymentIntegrationService
{
    public Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentResult
        {
            IsSuccess = true,
            TransactionId = Guid.NewGuid().ToString("N"),
        });
    }
}
