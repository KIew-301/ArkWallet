namespace ArkWallet.Presentation.DTOs;

/// <summary>
/// Ответ со списком доступных подписок.
/// </summary>
/// <param name="Subscriptions">Массив подписок.</param>
public record SubscriptionsResponse(SubscriptionResponse[] Subscriptions);

/// <summary>
/// Информация о подписке для клиента.
/// </summary>
/// <param name="Id">Идентификатор подписки.</param>
/// <param name="Name">Название подписки.</param>
/// <param name="Level">Уровень подписки.</param>
/// <param name="PriceWeekRubles">Стоимость подписки на неделю в рублях.</param>
/// <param name="PriceMonthRubles">Стоимость подписки на месяц в рублях.</param>
/// <param name="PriceYearRubles">Стоимость подписки на год в рублях.</param>
/// <param name="MaxOrders">Максимальное количество ордеров.</param>
/// <param name="MaxMiningMachines">Максимальное количество майнинг-машин.</param>
/// <param name="DurationMinutes">Длительность подписки в минутах (null — бессрочная).</param>
public record SubscriptionResponse(
    int Id,
    string Name,
    int Level,
    decimal PriceWeekRubles,
    decimal PriceMonthRubles,
    decimal PriceYearRubles,
    int MaxOrders,
    int MaxMiningMachines,
    int? DurationMinutes);

/// <summary>
/// Запрос на покупку подписки.
/// </summary>
public class PurchaseSubscriptionRequest
{
    /// <summary>
    /// Идентификатор приобретаемой подписки.
    /// </summary>
    public int SubscriptionId { get; init; }

    /// <summary>
    /// Период покупки: week/month/year (или неделя/месяц/год).
    /// </summary>
    public string Period { get; init; } = string.Empty;
}

/// <summary>
/// Результат покупки подписки.
/// </summary>
public record PurchaseSubscriptionResponse(
    bool Success,
    string? Message,
    string? TransactionId,
    DateTime? ExpiresAtUtc);
