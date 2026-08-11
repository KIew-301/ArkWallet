using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;

namespace ArkWallet.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор снятия собранных токенов со слотов майнинг-машин
/// </summary>
public interface IMiningMachineSlotTakingTokenOrchestrator
{
    /// <summary>
    /// Снимает собранные токены с одной машины и добавляет их в портфель трейдера
    /// </summary>
    Task<Result> TakeTokensFromMachineAsync(long traderId, long miningMachineId);

    /// <summary>
    /// Снимает собранные токены со всех машин трейдера и добавляет их в портфель
    /// </summary>
    Task<Result> TakeTokensFromMachinesAsync(long traderId);
}
