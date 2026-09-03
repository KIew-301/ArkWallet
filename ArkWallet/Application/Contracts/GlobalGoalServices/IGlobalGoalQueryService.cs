using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.GlobalGoalServices;

/// <summary>
/// Сервис запроса глобальных целей.
/// </summary>
public interface IGlobalGoalQueryService
{
    /// <summary>
    /// Загружает и возвращает чёткую структуру глобальных целей.
    /// </summary>
    Task<Result<List<GlobalGoalInfo>>> GetGoalsAsync();
}

/// <summary>
/// Чёткая структура глобальной цели для запроса.
/// </summary>
public record GlobalGoalInfo(
    long Id,
    string Name,
    string Description,
    decimal Target,
    decimal Actual,
    decimal Progress,
    int AchievedCount,
    List<GlobalGoalStepInfo> Steps
);

/// <summary>
/// Промежуточный этап глобальной цели с собственной наградой.
/// </summary>
public record GlobalGoalStepInfo(
    int StepNumber,
    decimal Target,
    string SymbolForReward,
    decimal AmountForReward
);
