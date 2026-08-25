using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.Orchestrators;

/// <summary>
/// Оркестратор продажи слота майнинг-машины
/// </summary>
public interface IMiningMachineSlotSellingOrchestrator
{
    /// <summary>
    /// Продаёт слот: зачисляет выручку, переводит в статус sold и возвращает собранные токены в портфель
    /// </summary>
    Task<Result> SellMachineAsync(long traderId, long miningMachineSlotId);
}
