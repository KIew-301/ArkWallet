using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис создания и обновления глобальных правил майнинга токенов
/// </summary>
public interface IMiningGlobalRuleCreationService
{
    /// <summary>
    /// Создаёт или обновляет глобальные правила для всех токенов:
    /// сдвигает коэффициенты, генерирует будущий коэффициент и базовую скорость
    /// </summary>
    /// <returns>Результат операции</returns>
    Task<Result> CreateRulesAsync();
}
