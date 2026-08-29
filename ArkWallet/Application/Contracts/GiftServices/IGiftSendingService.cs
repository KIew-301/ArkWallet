using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.GiftServices;

/// <summary>
/// Сервис для отправки подарков
/// </summary>
public interface IGiftSendingService
{
    /// <summary>
    /// Отправить подарок (токен выбирается случайно из портфеля, 1 штука)
    /// </summary>
    /// <param name="senderId">Telegram ID отправителя</param>
    /// <param name="recipientId">Telegram ID получателя</param>
    Task<Result<GiftSendResult>> SendGiftAsync(long senderId, long recipientId);
}

/// <summary>
/// Результат отправки подарка
/// </summary>
public record GiftSendResult(
    Guid GiftId,
    long SenderId,
    long RecipientId,
    string TokenSymbol,
    decimal Quantity,
    decimal PriceAtSend
);
