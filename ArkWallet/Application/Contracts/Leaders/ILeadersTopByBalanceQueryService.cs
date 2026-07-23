using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.Leaders;

/// <summary>
/// Сервис для получения рейтинга трейдеров по общему балансу
/// </summary>
public interface ILeadersTopByBalanceQueryService
{
    /// <summary>
    /// Возвращает топ трейдеров по общему балансу
    /// </summary>
    /// <param name="count">Количество трейдеров в топе</param>
    Task<Result<List<LeaderEntry>>> GetTopAsync(int count);

    /// <summary>
    /// Возвращает позицию трейдера в рейтинге
    /// </summary>
    /// <param name="traderId">Telegram ID трейдера</param>
    Task<Result<LeaderPosition>> GetTraderPositionAsync(long traderId);

    /// <summary>
    /// Возвращает локальный топ вокруг трейдера (выше и ниже по рейтингу)
    /// </summary>
    /// <param name="traderId">Telegram ID трейдера</param>
    /// <param name="aboveCount">Сколько трейдеров показать выше</param>
    /// <param name="belowCount">Сколько трейдеров показать ниже</param>
    Task<Result<List<LeaderEntry>>> GetLocalTopAsync(long traderId, int aboveCount, int belowCount);
}

/// <summary>
/// DTO для отображения одного трейдера в рейтинге
/// </summary>
/// <param name="Position">Позиция в рейтинге (1 = первый)</param>
/// <param name="TraderId">Telegram ID трейдера</param>
/// <param name="Username">Имя пользователя</param>
/// <param name="TotalBalance">Общий баланс</param>
public record LeaderEntry(int Position, long TraderId, string Username, decimal TotalBalance);

/// <summary>
/// DTO с позицией трейдера в рейтинге
/// </summary>
/// <param name="Position">Позиция в рейтинге</param>
/// <param name="TotalTraders">Общее количество трейдеров в рейтинге</param>
/// <param name="TotalBalance">Общий баланс трейдера</param>
public record LeaderPosition(int Position, int TotalTraders, decimal TotalBalance);
