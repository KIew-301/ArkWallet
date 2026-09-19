using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseHistoryServices;

/// <summary>
/// Сервис запросов истории покупок подписок.
/// </summary>
public interface IPurchaseHistoryQueryService
{
    /// <summary>
    /// Возвращает полную историю покупок подписок для указанного пользователя.
    /// </summary>
    /// <param name="traderTelegramId">ID телеграм-пользователя.</param>
    /// <returns>Успешный результат со списком записей истории покупок или ошибка.</returns>
    Task<Result<List<PurchaseHistoryEntry>>> GetHistoryAsync(long traderTelegramId);
}
