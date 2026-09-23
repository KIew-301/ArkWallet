namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;

/// <summary>
/// Запрос на создание платежа для подписки.
/// </summary>
public class PaymentRequest
{
    /// <summary>
    /// ID телеграм-пользователя (трейдера), инициирующего платеж.
    /// </summary>
    public long TraderTelegramId { get; init; }

    /// <summary>
    /// Сумма платежа в рублях.
    /// </summary>
    public decimal AmountRubles { get; init; }

    /// <summary>
    /// ID подписки, на которую направлен платеж.
    /// </summary>
    public int SubscriptionId { get; init; }

    /// <summary>
    /// Описание платежа (необязательное).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Период подписки (неделя/месяц/год) как int.
    /// </summary>
    public int Period { get; init; }

    /// <summary>
    /// Сохранить платёжный метод для автопродления (первый платёж).
    /// </summary>
    public bool SavePaymentMethod { get; init; }

    /// <summary>
    /// ID сохранённого платёжного метода ЮKassa для рекуррентного списания (автопродление).
    /// Если задан, платёж выполняется без участия пользователя.
    /// </summary>
    public string? PaymentMethodId { get; init; }
}

/// <summary>
/// Результат создания платежа.
/// </summary>
public class PaymentResult
{
    /// <summary>
    /// ID транзакции в платёжной системе.
    /// </summary>
    public string? TransactionId { get; init; }

    /// <summary>
    /// Указывает, успешно ли создан платёж.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Дополнительная информация о плательщике (необязательное).
    /// </summary>
    public string? PayerInfo { get; init; }

    /// <summary>
    /// ID платежа во внешней платёжной системе.
    /// </summary>
    public string? PaymentId { get; init; }

    /// <summary>
    /// Ссылка на платёжную форму для оплаты (null, если платеж оплачен сразу).
    /// </summary>
    public string? ConfirmationUrl { get; init; }

    /// <summary>
    /// Требуется ли подтверждение платежа (двухстадийный флоу). true = нужно дождаться оплаты, false = уже оплачен.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// ID сохранённого платёжного метода ЮKassa (для автопродления), если был сохранён.
    /// </summary>
    public string? SavedPaymentMethodId { get; init; }
}

/// <summary>
/// Текущий статус платежа во внешней платёжной системе.
/// </summary>
public class PaymentStatusResult
{
    /// <summary>
    /// ID платежа во внешней платёжной системе.
    /// </summary>
    public string? PaymentId { get; init; }

    /// <summary>
    /// сырой статус платёжной системы ("pending" | "succeeded" | "canceled" | "expired").
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// ID сохранённого платёжного метода ЮKassa (для автопродления), если был сохранён.
    /// </summary>
    public string? SavedPaymentMethodId { get; init; }

    /// <summary>
    /// Платёж успешно оплачен.
    /// </summary>
    public bool IsSucceeded => Status == "succeeded";

    /// <summary>
    /// Платёж отменён или истёк.
    /// </summary>
    public bool IsCanceled => Status is "canceled" or "expired";
}
