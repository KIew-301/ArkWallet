using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор для управления MarketWallBlocker-ботом (трейдер 103)
/// </summary>
public interface IMarketWallBlockerOrchestrator
{
    /// <summary>
    /// Проверяет и регистрирует трейдера 103, если его нет
    /// </summary>
    /// <returns>Результат операции</returns>
    Task<Result> EnsureRegisteredAsync();

    /// <summary>
    /// Восполняет баланс и токены трейдера 103 до целевых значений
    /// </summary>
    /// <returns>Результат операции</returns>
    Task<Result> EnsureBalancesAsync();

    /// <summary>
    /// Выполняет итерацию бота: отменяет все свои ордера и выставляет новые на уровнях WallBlockerEngine
    /// </summary>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// Итерация выполняется по расписанию (следующее обновление через 45-140 минут).
    /// Если время ещё не пришло, возвращает Ok без действий.
    /// </remarks>
    Task<Result> ExecuteIterationAsync();
}
