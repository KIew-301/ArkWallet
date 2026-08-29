using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.GiftServices;

/// <summary>
/// Сервис для получения подарков
/// </summary>
public interface IGiftReceivingService
{
    /// <summary>
    /// Принять подарок
    /// </summary>
    /// <param name="recipientId">Telegram ID получателя</param>
    /// <param name="giftId">ID подарка</param>
    Task<Result<GiftReceiveResult>> ReceiveGiftAsync(long recipientId, Guid giftId);

    /// <summary>
    /// Принять все подарки
    /// </summary>
    /// <param name="recipientId">Telegram ID получателя</param>
    Task<Result<GiftReceiveAllResult>> ReceiveAllGiftsAsync(long recipientId);
}

public record GiftReceiveResult(
    Guid GiftId,
    long SenderId,
    long RecipientId,
    string TokenSymbol,
    decimal Quantity
);

public record GiftReceiveAllResult(
    long RecipientId,
    int Count,
    IReadOnlyList<GiftReceiveResult> Gifts
);
