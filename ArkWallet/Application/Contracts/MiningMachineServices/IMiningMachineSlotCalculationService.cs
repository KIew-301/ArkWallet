using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис расчёта накопления токенов на слоты майнинг-машин
/// </summary>
public interface IMiningMachineSlotCalculationService
{
    /// <summary>
    /// Начисляет токены на все активные слоты по формуле:
    /// cash = глобальный коэффициент * коэффициент машины * timingCoeff * базовая скорость
    /// </summary>
    /// <param name="timingCoeff">Коэффициент времени с последнего расчёта</param>
    /// <returns>Результат с количеством обработанных слотов</returns>
    Task<Result<int>> TakeTokensOnMachinesAsync(decimal timingCoeff = 1);
}
