using ArkWallet.Core.General.Application.Common;

namespace ArkWallet.Core.General.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор переключения слота майнинг-машины на другой токен
/// </summary>
public interface IMiningMachineSlotSwitchingOrchestrator
{
    /// <summary>
    /// Запускает переключение слота на другой токен и возвращает собранные токены в портфель
    /// </summary>
    Task<Result> SwitchTargetTokenAsync(long traderId, long miningMachineSlotId, string symbol);
}
