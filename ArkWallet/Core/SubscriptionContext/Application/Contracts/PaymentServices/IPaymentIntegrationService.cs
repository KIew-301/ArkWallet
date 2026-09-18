namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;

public interface IPaymentIntegrationService
{
    Task<PaymentResult> CreatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}
