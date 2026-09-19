using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Infrastructure.Payment;

/// <summary>
/// Сервис платежей, который мгновенно подтверждает любой платёж без обращения к внешней платёжной системе.
/// Используется как заглушка/тестовый режим (оплата в один клик, без ссылки на платёжную форму).
/// </summary>
public class InstantSuccessPaymentService : IPaymentIntegrationService
{
    /// <summary>
    /// Мгновенно создаёт успешный платёж: подписка выдаётся сразу, подтверждение не требуется.
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
            PaymentId = Guid.NewGuid().ToString("N"),
            RequiresConfirmation = false,
        });
    }

    /// <summary>
    /// Заглушка: любой существующий платёж считается успешно оплаченным.
    /// </summary>
    /// <param name="externalPaymentId">ID платежа (во внешней системе не хранится, параметр просто пробрасывается).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Успешный статус платежа.</returns>
    public Task<PaymentStatusResult> GetPaymentStatusAsync(
        string externalPaymentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentStatusResult
        {
            PaymentId = externalPaymentId,
            Status = "succeeded",
        });
    }
}
