using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;

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
    /// <param name="subscriptionId">ID приобретёемой подписки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат покупки (успех или ошибка).</returns>
    Task<PurchaseResult> PurchaseAsync(long traderTelegramId, int subscriptionId, CancellationToken cancellationToken = default);
}
