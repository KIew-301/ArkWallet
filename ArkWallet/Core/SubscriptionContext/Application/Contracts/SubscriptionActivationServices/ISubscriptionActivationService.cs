using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;

/// <summary>
/// Сервер активации подписки.
/// </summary>
public interface ISubscriptionActivationService
{
    /// <summary>
    /// Активирует подписку для трейдера.
    /// </summary>
    /// <param name="traderTelegramId">ID трейдера в Telegram.</param>
    /// <param name="subscriptionId">Идентификатор подписки.</param>
    /// <param name="period">Период подписки.</param>
    /// <param name="amountRubles">Сумма в рублях.</param>
    /// <param name="transactionId">ID транзакции.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат активации.</returns>
    Task<SubscriptionActivationResult> ActivateAsync(long traderTelegramId, int subscriptionId, SubscriptionPeriod period, decimal amountRubles, string? transactionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат активации подписки.
/// </summary>
/// <param name="Success">Указывает, успешна ли активация.</param>
/// <param name="Expiry">Дата истечения подписки (null — бессрочная).</param>
public sealed record SubscriptionActivationResult(bool Success, DateTime? Expiry);
