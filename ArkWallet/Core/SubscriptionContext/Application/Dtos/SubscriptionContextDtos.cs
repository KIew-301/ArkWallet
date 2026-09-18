namespace ArkWallet.Core.SubscriptionContext.Application.Dtos;

public record SubscriptionInfo(
    int Id,
    string Name,
    int Level,
    decimal PriceRubles,
    int MaxOrders,
    int MaxMiningMachines,
    int? DurationMinutes);

public record PurchaseResult(bool Success, string Message, string? TransactionId, DateTime? ExpiresAtUtc)
{
    public static PurchaseResult Fail(string message) => new(false, message, null, null);
}

public record PurchaseHistoryEntry(
    long Id,
    long TraderId,
    int SubscriptionId,
    string SubscriptionName,
    decimal PriceRubles,
    DateTime PurchasedAtUtc,
    DateTime? ExpiresAtUtc,
    string? TransactionId);
