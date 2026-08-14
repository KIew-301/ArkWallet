using ArkWallet.Application.Common;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис чтения данных майнинг-машин
/// </summary>
public interface IMiningMachineQueryService
{
    /// <summary>
    /// Получает все машины, доступные для покупки, кроме уже купленных трейдером, с данными по майнингу токенов
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера, чьи купленные машины исключаются</param>
    /// <returns>Результат со списком машин, отсортированных по цене от дешёвых к дорогим</returns>
    Task<Result<List<MiningMachineData>>> TakeActiveForSaleMachinesAsync(long traderId);
}
