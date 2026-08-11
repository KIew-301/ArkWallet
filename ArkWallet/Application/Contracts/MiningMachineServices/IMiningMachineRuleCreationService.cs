using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис для создания правил майнинга (связка машина-токен)
/// </summary>
public interface IMiningMachineRuleCreationService
{
    /// <summary>
    /// Создаёт одно правило майнинга
    /// </summary>
    /// <returns>Результат с идентификатором созданного правила</returns>
    Task<Result<long>> CreateRuleAsync(MiningMachineRuleCreationCommand command);

    /// <summary>
    /// Создаёт несколько правил майнинга пакетно
    /// </summary>
    /// <returns>Результат со списком созданных правил</returns>
    Task<Result<List<long>>> CreateRulesAsync(IEnumerable<MiningMachineRuleCreationCommand> commands);
}

/// <summary>
/// Команда создания правила майнинга
/// </summary>
/// <param name="MiningMachineId">Идентификатор машины</param>
/// <param name="CharacterTokenId">Символ токена</param>
/// <param name="MiningCoefficient">Коэффициент майнинга</param>
public record MiningMachineRuleCreationCommand(
    long MiningMachineId,
    string CharacterTokenId,
    decimal MiningCoefficient);
