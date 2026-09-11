using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices;

namespace ArkWallet.Core.ShoppingContext.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор создания майнинг-машин вместе с их правилами
/// </summary>
public interface IMiningMachineCreationOrchestrator
{
    /// <summary>
    /// Создаёт майнинг-машину и её правила в одной транзакции
    /// </summary>
    Task<Result<MiningMachineCreationData>> CreateMachineAsync(MiningMachineCreationCommand command);

    /// <summary>
    /// Создаёт несколько майнинг-машин и их правила в одной транзакции
    /// </summary>
    Task<Result<List<MiningMachineCreationData>>> CreateMachinesAsync(IEnumerable<MiningMachineCreationCommand> commands);
}
