using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис удаления правил майнинга конкретных токенов на майнинг-машинах
/// </summary>
public interface IMiningMachineRuleDeletionService
{
    /// <summary>
    /// Удаление правила майнинга. Невозможно, если правило используется слотом машины.
    /// </summary>
    /// <param name="ruleId">Идентификатор правила майнинга</param>
    /// <returns>Результат операции удаления</returns>
    Task<Result> DeleteRuleAsync(long ruleId);
}
