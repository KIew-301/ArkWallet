using ArkWallet.Core.General.Application.Common;

namespace ArkWallet.Core.MailContext.Application.Contracts.MailServices;

/// <summary>
/// Сервис запросов писем пользователя
/// </summary>
public interface IQueryService
{
    /// <summary>
    /// Возвращает все письма пользователя
    /// </summary>
    Task<Result<List<MailInfo>>> GetUserMailsAsync(long traderId);

    /// <summary>
    /// Возвращает страницу писем пользователя, свежие первыми
    /// </summary>
    Task<Result<PagedResult<MailInfo>>> GetUserMailsPageAsync(
        long traderId,
        MailFilter filter,
        int page,
        int pageSize);

    /// <summary>
    /// Возвращает одно письмо пользователя
    /// </summary>
    Task<Result<MailInfo>> GetUserMailAsync(long traderId, long mailId);

    /// <summary>
    /// Возвращает счётчики писем пользователя для вкладок фильтра
    /// </summary>
    Task<Result<MailCounters>> GetMailCountersAsync(long traderId);
}

/// <summary>
/// Фильтр писем пользователя
/// </summary>
public enum MailFilter
{
    /// <summary>Все письма</summary>
    All,

    /// <summary>Только непрочитанные</summary>
    Unread,

    /// <summary>Только с доступной наградой</summary>
    Reward
}

/// <summary>
/// Счётчики писем пользователя
/// </summary>
/// <param name="UnreadCount">Количество непрочитанных писем</param>
/// <param name="RewardCount">Количество писем с доступной наградой</param>
/// <param name="TotalCount">Общее количество писем</param>
public record MailCounters(int UnreadCount, int RewardCount, int TotalCount);

/// <summary>
/// Информация о письме
/// </summary>
public record MailInfo(
    long Id,
    long TraderId,
    string Title,
    string Message,
    string SenderName,
    long? SenderId,
    string SymbolForReward,
    decimal AmountForReward,
    string Status,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? AcceptedAt
)
{
    /// <summary>
    /// Есть ли в письме доступная награда
    /// </summary>
    public bool HasReward => !string.IsNullOrEmpty(SymbolForReward) && AmountForReward > 0;
}
