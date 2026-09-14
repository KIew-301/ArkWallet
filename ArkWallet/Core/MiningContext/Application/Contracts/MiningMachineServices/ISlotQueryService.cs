using ArkWallet.Core.MiningContext.Application.Dtos;
using ArkWallet.Core.General.Application.Common;

namespace ArkWallet.Core.MiningContext.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис чтения данных слотов майнинг-машин трейдера
/// </summary>
public interface IMiningMachineSlotQueryService
{
    /// <summary>
    /// Получает слоты указанного трейдера (без проданных)
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера</param>
    /// <returns>Результат со списком слотов, отсортированных по дате создания от поздних к ранним</returns>
    Task<Result<List<MiningMachineSlotData>>> TakeSlotsByTraderAsync(long traderId);
}
