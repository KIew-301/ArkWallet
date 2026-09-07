using ArkWallet.Core.General.Application.Common;

namespace ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;

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
