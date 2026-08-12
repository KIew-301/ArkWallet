using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис ручного обновления глобальных правил майнинга токена
/// </summary>
public interface IMiningGlobalRuleUpdateService
{
    /// <summary>
    /// Обновляет коэффициенты (текущий и будущий) и/или базовую скорость майнинга
    /// для глобального правила токена. Коэффициенты задаются только парой.
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="currentCoefficient">Новый текущий коэффициент (опционально)</param>
    /// <param name="futureCoefficient">Новый будущий коэффициент (опционально)</param>
    /// <param name="baseMiningSpeed">Новая базовая скорость добычи (опционально)</param>
    /// <returns>Результат операции обновления</returns>
    Task<Result> UpdateRuleAsync(string symbol, decimal? currentCoefficient, decimal? futureCoefficient, decimal? baseMiningSpeed);
}
