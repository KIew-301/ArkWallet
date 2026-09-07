using ArkWallet.Application.Common;

namespace ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;

/// <summary>
/// Сервис создания глобальных целей и их промежуточных шагов.
/// </summary>
public interface IGlobalGoalCreationService
{
    /// <summary>
    /// Создаёт новую глобальную цель.
    /// </summary>
    Task<Result<long>> CreateGoalAsync(CreateGlobalGoalCommand command);

    /// <summary>
    /// Добавляет промежуточный шаг к существующей глобальной цели.
    /// </summary>
    Task<Result> AddStepAsync(AddGlobalGoalStepCommand command);
}

/// <summary>
/// Команда создания глобальной цели. Начальный рубеж (шаг 1) задаётся вместе с целью.
/// </summary>
public record CreateGlobalGoalCommand(
    string Name,
    string Description,
    decimal Target,
    string SymbolForReward,
    decimal AmountForReward);

/// <summary>
/// Команда добавления промежуточного шага глобальной цели.
/// </summary>
public record AddGlobalGoalStepCommand(
    long GoalId,
    int StepNumber,
    decimal Target,
    string SymbolForReward,
    decimal AmountForReward);