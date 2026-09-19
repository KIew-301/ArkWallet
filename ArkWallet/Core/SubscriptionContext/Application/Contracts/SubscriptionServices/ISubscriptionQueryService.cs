using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;

/// <summary>
/// Сервис запросов информации о подписках.
/// </summary>
public interface ISubscriptionQueryService
{
    /// <summary>
    /// Возвращает список всех доступных подписок.
    /// </summary>
    /// <returns>Успешный результат со списком подписок или ошибка.</returns>
    Task<Result<List<SubscriptionInfo>>> GetAllAsync();

    /// <summary>
    /// Возвращает базовую (первую найденную) подписку.
    /// </summary>
    /// <returns>Информация о базовой подписке или null, если подписки отсутствуют.</returns>
    Task<SubscriptionInfo?> GetBasicAsync();

    /// <summary>
    /// Возвращает информацию о подписке по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор подписки.</param>
    /// <returns>Результат с информацией о подписке или ошибка.</returns>
    Task<Result<SubscriptionInfo?>> GetByIdAsync(int id);
}
