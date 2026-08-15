using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис для изменения правил майнинга (связка машина-токен).
/// После изменения пересчитываются имя и стоимость машины.
/// </summary>
public interface IMiningMachineRuleUpdateService
{
    /// <summary>
    /// Обновляет коэффициент майнинга правила
    /// </summary>
    /// <returns>Результат операции</returns>
    Task<Result> UpdateRuleAsync(MiningMachineRuleUpdateCommand command);
}

/// <summary>
/// Команда изменения правила майнинга
/// </summary>
/// <param name="MiningRuleId">Идентификатор правила майнинга</param>
/// <param name="MiningCoefficient">Новый коэффициент майнинга</param>
public record MiningMachineRuleUpdateCommand(
    long MiningRuleId,
    decimal MiningCoefficient);
