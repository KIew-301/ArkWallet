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

/// <summary>
/// Результат приёма одного подарка
/// </summary>
/// <param name="GiftId">ID подарка</param>
/// <param name="SenderId">Telegram ID отправителя</param>
/// <param name="RecipientId">Telegram ID получателя</param>
/// <param name="TokenSymbol">Символ токена</param>
/// <param name="Quantity">Количество токенов</param>
public record GiftReceiveResult(
    Guid GiftId,
    long SenderId,
    long RecipientId,
    string TokenSymbol,
    decimal Quantity
);

/// <summary>
/// Результат приёма всех подарков
/// </summary>
/// <param name="RecipientId">Telegram ID получателя</param>
/// <param name="Count">Количество принятых подарков</param>
/// <param name="Gifts">Список принятых подарков</param>
public record GiftReceiveAllResult(
    long RecipientId,
    int Count,
    IReadOnlyList<GiftReceiveResult> Gifts
);
