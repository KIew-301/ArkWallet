namespace ArkWallet.Core.SubscriptionContext.Application.Dtos;

/// <summary>
/// Информационная DTO-запись о подписке.
/// </summary>
/// <param name="Id">Идентификатор подписки.</param>
/// <param name="Name">Название подписки.</param>
/// <param name="Level">Уровень подписки.</param>
/// <param name="PriceRubles">Базовая стоимость в рублях (legacy).</param>
/// <param name="PriceWeekRubles">Стоимость подписки на неделю в рублях.</param>
/// <param name="PriceMonthRubles">Стоимость подписки на месяц в рублях.</param>
/// <param name="PriceYearRubles">Стоимость подписки на год в рублях.</param>
/// <param name="MaxOrders">Максимальное количество ордеров.</param>
/// <param name="MaxMiningMachines">Максимальное количество майнинг-машин.</param>
/// <param name="DurationMinutes">Длительность подписки в минутах (null — бессрочная).</param>
public record SubscriptionInfo(
    int Id,
    string Name,
    int Level,
    decimal PriceRubles,
    decimal PriceWeekRubles,
    decimal PriceMonthRubles,
    decimal PriceYearRubles,
    int MaxOrders,
    int MaxMiningMachines,
    int? DurationMinutes);

/// <summary>
/// Результат операции покупки подписки.
/// </summary>
/// <param name="Success">Указывает, успешна ли покупка.</param>
/// <param name="Message">Описание результата (сообщение об успехе или ошибке).</param>
/// <param name="TransactionId">ID транзакции (только при успехе).</param>
/// <param name="ExpiresAtUtc">Дата истечения подписки (только при успехе).</param>
public record PurchaseResult(bool Success, string Message, string? TransactionId, DateTime? ExpiresAtUtc)
{
    /// <summary>
    /// Создает результат с ошибкой (неуспешная покупка).
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    /// <returns>Неуспешный результат покупки.</returns>
    public static PurchaseResult Fail(string message) => new(false, message, null, null);
}

/// <summary>
/// Запись в истории покупок подписок.
/// </summary>
/// <param name="Id">Идентификатор записи в истории.</param>
/// <param name="TraderId">ID трейдера, совершившего покупку.</param>
/// <param name="SubscriptionId">ID купленной подписки.</param>
/// <param name="SubscriptionName">Название купленной подписки.</param>
/// <param name="PriceRubles">Сумма purchase в рублях.</param>
/// <param name="PurchasedAtUtc">Дата и время покупки (UTC).</param>
/// <param name="ExpiresAtUtc">Дата истечения подписки (UTC, может быть null).</param>
/// <param name="TransactionId">ID транзакции.</param>
public record PurchaseHistoryEntry(
    long Id,
    long TraderId,
    int SubscriptionId,
    string SubscriptionName,
    decimal PriceRubles,
    DateTime PurchasedAtUtc,
    DateTime? ExpiresAtUtc,
    string? TransactionId);
