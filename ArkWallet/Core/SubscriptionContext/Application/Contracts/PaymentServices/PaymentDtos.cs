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
}
