using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис покупки майнинг-машины трейдером
/// </summary>
public interface IMiningMachineSlotBuyingService
{
    /// <summary>
    /// Покупает майнинг-машину: списывает деньги и создаёт слот в статусе passive
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера</param>
    /// <param name="machineId">Идентификатор машины</param>
    /// <returns>Результат с идентификатором созданного слота</returns>
    Task<Result<long>> BuyMachineAsync(long traderId, long machineId);
}
