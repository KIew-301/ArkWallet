using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MailServices;

/// <summary>
/// Сервис запросов писем пользователя
/// </summary>
public interface IMailQueryService
{
    /// <summary>
    /// Возвращает все письма пользователя
    /// </summary>
    Task<Result<List<MailInfo>>> GetUserMailsAsync(long traderId);
}

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
);
