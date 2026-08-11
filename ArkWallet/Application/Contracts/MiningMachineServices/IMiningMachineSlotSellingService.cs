using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис продажи слота майнинг-машины
/// </summary>
public interface IMiningMachineSlotSellingService
{
    /// <summary>
    /// Продаёт слот: зачисляет выручку на баланс, переводит слот в статус sold.
    /// Возвращает собранные токены для возврата в портфель.
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера</param>
    /// <param name="miningMachineSlotId">Идентификатор слота</param>
    /// <returns>Результат с символом токена и количеством собранных токенов</returns>
    Task<Result<MiningTokenCollectionResult>> SellMachineAsync(long traderId, long miningMachineSlotId);
}
