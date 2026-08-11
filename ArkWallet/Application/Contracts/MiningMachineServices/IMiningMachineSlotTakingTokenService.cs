using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис снятия собранных токенов со слотов майнинг-машин
/// </summary>
public interface IMiningMachineSlotTakingTokenService
{
    /// <summary>
    /// Снимает собранные токены с одной машины трейдера (только целую часть)
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера</param>
    /// <param name="miningMachineId">Идентификатор слота машины</param>
    /// <returns>Результат со снятыми токенами</returns>
    Task<Result<MiningTokenCollectionResult>> TakeTokensFromMachineAsync(long traderId, long miningMachineId);

    /// <summary>
    /// Снимает собранные токены со всех машин трейдера (только целую часть)
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера</param>
    /// <returns>Результат со списком снятых токенов</returns>
    Task<Result<List<MiningTokenCollectionResult>>> TakeTokensFromMachinesAsync(long traderId);
}
