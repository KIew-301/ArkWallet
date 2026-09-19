using System;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;

/// <summary>
/// Ближайшее будущее истечение подписки конкретного трейдера.
/// </summary>
/// <param name="TraderId">TelegramId трейдера.</param>
/// <param name="ExpiresAtUtc">Дата и время UTC истечения подписки.</param>
public sealed record TraderSubscriptionExpiry(long TraderId, DateTime ExpiresAtUtc);

/// <summary>
/// Сервис обработки истечения сроков действия подписок.
/// </summary>
public interface ISubscriptionExpiryService
{
    /// <summary>
    /// Обрабатывает все просроченные подписки: деактивирует их и возвращает количество обработанных записей.
    /// Используется как фоллбэк при запуске воркера или после длительных простоев, когда массовый скан допустим.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Количество обработанных просроченных подписок.</returns>
    Task<int> ProcessExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ближайшую будущую дату истечения подписки вместе с Id трейдера, у которого она наступает.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Объект с Id трейдера и датой истечения либо null, если активных подписок нет.</returns>
    Task<TraderSubscriptionExpiry?> GetNextExpiryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Переводит одного трейдера на базовую (бессрочную) подписку, если его подписка реально истекла.
    /// </summary>
    /// <param name="traderId">TelegramId трейдера для перевода.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>1, если перевод выполнен (истёкший трейдер найден), иначе 0.</returns>
    Task<int> DowngradeToBasicAsync(long traderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переводит массив трейдеров на базовую (бессрочную) подписку. Снимает дубликатов, применяет защитный фильтр по дате истечения.
    /// </summary>
    /// <param name="traderIds">Массив TelegramId трейдеров для перевода.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Количество успешно переведённых (истёкших) трейдеров.</returns>
    Task<int> DowngradeToBasicAsync(long[] traderIds, CancellationToken cancellationToken = default);
}
