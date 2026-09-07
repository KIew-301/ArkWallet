using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MailServices;

/// <summary>
/// Сервис обновления статусов писем
/// </summary>
public interface IMailStatusUpdatingService
{
    /// <summary>
    /// Пометить письмо как прочитанное
    /// </summary>
    Task<Result> MarkAsReadAsync(long mailId, long traderId);

    /// <summary>
    /// Пометить письмо как принятое (награда принята)
    /// </summary>
    Task<Result> MarkAsAcceptedAsync(long mailId, long traderId);
}
