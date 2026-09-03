using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.GlobalGoalServices;

/// <summary>
/// Сервис проверки глобальных целей.
/// </summary>
public interface IGlobalGoalCheckingService
{
    /// <summary>
    /// Проверяет все глобальные цели на достижение, фиксирует историю при достижении.
    /// </summary>
    Task<Result> CheckGoalsAsync();
}
