using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Infrastructure.Payment;

/// <summary>
/// Сервис платежей, который мгновенно подтверждает любой платёж без обращения к внешней платёжной системе.
/// Используется как заглушка/тестовая реализация.
/// </summary>
public class InstantSuccessPaymentService : IPaymentIntegrationService
{
    /// <summary>
    /// Мгновенно создаёт успешный платёж без верификации.
    /// </summary>
    /// <param name="request">Запрос на создание платежа.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Успешный результат платежа с новым ID транзакции.</returns>
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
