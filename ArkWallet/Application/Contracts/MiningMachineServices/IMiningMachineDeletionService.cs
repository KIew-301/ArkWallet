using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис удаления и деактивации майнинг-машин
/// </summary>
public interface IMiningMachineDeletionService
{
    /// <summary>
    /// Полное удаление майнинг-машины вместе с её правилами.
    /// Невозможно, если на машину существуют слоты.
    /// </summary>
    /// <param name="machineId">Идентификатор майнинг-машины</param>
    /// <returns>Результат операции удаления</returns>
    Task<Result> DeleteMachineAsync(long machineId);

    /// <summary>
    /// Мягкое отключение майнинг-машины: IsActiveForSale = false.
    /// Машина остаётся в БД, но исчезает из списка доступных для покупки.
    /// </summary>
    /// <param name="machineId">Идентификатор майнинг-машины</param>
    /// <returns>Результат операции деактивации</returns>
    Task<Result> DeactivateMachineAsync(long machineId);
}
