using ArkWallet.Infrastructure.Data;

namespace ArkWallet.Application.Contracts.GlobalGoalServices;

/// <summary>
/// Расчёт значения конкретной глобальной цели. Каждая реализация имеет доступ к БД
/// и рассчитывает текущее значение для своей цели (доступ по имени цели).
/// </summary>
internal interface IDomainGlobalGoalCalculation
{
    /// <summary>
    /// Имя цели, значение которой рассчитывает эта реализация.
    /// </summary>
    string GoalName { get; }

    /// <summary>
    /// Рассчитывает текущее значение цели.
    /// </summary>
    Task<decimal> CalculateAsync(ArkWalletDbContext dbContext);
}
