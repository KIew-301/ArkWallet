namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;

/// <summary>
/// Сервис обработки истечения сроков действия подписок.
/// </summary>
public interface ISubscriptionExpiryService
{
    /// <summary>
    /// Обрабатывает все просроченные подписки: деактивирует их и возвращает количество обработанных записей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Количество обработанных просроченных подписок.</returns>
    Task<int> ProcessExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ближайшую дату истечения активной подписки.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Дата ближайшего истечения или null, если активных подписок нет.</returns>
    Task<DateTime?> GetNextExpiryAsync(CancellationToken cancellationToken = default);
}
