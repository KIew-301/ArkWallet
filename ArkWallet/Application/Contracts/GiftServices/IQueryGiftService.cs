using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.GiftServices;

/// <summary>
/// Сервис для запросов данных о подарках
/// </summary>
public interface IQueryGiftService
{
    /// <summary>
    /// Возвращает список подарков, ожидающих получения (статус "Sent") для получателя
    /// </summary>
    /// <param name="recipientId">Telegram ID получателя</param>
    /// <returns>Список подарков</returns>
    Task<Result<List<GiftInfo>>> GetPendingGiftsAsync(long recipientId);
}

/// <summary>
/// Информация о подарке
/// </summary>
/// <param name="GiftId">ID подарка</param>
/// <param name="SenderId">Telegram ID отправителя</param>
/// <param name="TokenSymbol">Символ токена</param>
/// <param name="Quantity">Количество</param>
/// <param name="SentAt">Дата отправки</param>
public record GiftInfo(
    Guid GiftId,
    long SenderId,
    string TokenSymbol,
    decimal Quantity,
    DateTime SentAt
);
