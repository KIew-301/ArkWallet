using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис чтения данных майнинг-машин
/// </summary>
public interface IMiningMachineQueryService
{
    /// <summary>
    /// Получает все машины, доступные для покупки, с данными по майнингу токенов
    /// </summary>
    /// <returns>Результат со списком машин, отсортированных по цене от дешёвых к дорогим</returns>
    Task<Result<List<MiningMachineData>>> TakeActiveForSaleMachinesAsync();
}
