using ArkWallet.Application.Common;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис чтения глобальных правил майнинга токенов
/// </summary>
public interface IMiningGlobalRuleQueryService
{
    /// <summary>
    /// Получает данные токенов и их глобальных правил майнинга со статусами
    /// </summary>
    /// <returns>Результат со списком правил, отсортированных по базовой прибыли от большего к меньшему</returns>
    Task<Result<List<TokensMiningRules>>> TakeRulesAsync();
}
