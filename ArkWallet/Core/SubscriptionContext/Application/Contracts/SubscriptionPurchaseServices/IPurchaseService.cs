using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;

/// <summary>
/// Сервис покупки подписок.
/// </summary>
public interface IPurchaseService
{
    /// <summary>
    /// Совершает покупку подписки для указанного пользователя.
    /// </summary>
    /// <param name="traderTelegramId">ID телеграм-пользователя, покупающего подписку.</param>
    /// <param name="subscriptionId">ID приобретёмой подписки.</param>
    /// <param name="period">Период покупки (неделя/месяц/год).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат покупки (успех или ошибка).</returns>
    Task<PurchaseResult> PurchaseAsync(long traderTelegramId, int subscriptionId, SubscriptionPeriod period, CancellationToken cancellationToken = default);
}
