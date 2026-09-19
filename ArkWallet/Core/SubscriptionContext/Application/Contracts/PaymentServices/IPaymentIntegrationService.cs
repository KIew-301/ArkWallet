namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;

/// <summary>
/// Сервис интеграции с платёжными системами для создания платежей.
/// </summary>
public interface IPaymentIntegrationService
{
    /// <summary>
    /// Создаёт новый платёж по запросу.
    /// </summary>
    /// <param name="request">Запрос на создание платежа.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат создания платежа.</returns>
    Task<PaymentResult> CreatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}
