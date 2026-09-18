namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;

public class PaymentRequest
{
    public long TraderTelegramId { get; init; }
    public decimal AmountRubles { get; init; }
    public int SubscriptionId { get; init; }
    public string? Description { get; init; }
}

public class PaymentResult
{
    public string TransactionId { get; init; }
    public bool IsSuccess { get; init; }
    public string? PayerInfo { get; init; }
}
