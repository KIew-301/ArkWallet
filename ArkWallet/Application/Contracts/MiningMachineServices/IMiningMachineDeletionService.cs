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
    /// Массово удаляет машины со всеми их правилами в одной транзакции.
    /// Откатывает всё, если хотя бы одна машина не существует.
    /// </summary>
    /// <param name="machineIds">Идентификаторы машин</param>
    /// <returns>Результат удаления</returns>
    Task<Result> DeleteMachinesAsync(long[] machineIds);

    /// <summary>
    /// Мягкое отключение майнинг-машины: IsActiveForSale = false.
    /// Машина остаётся в БД, но исчезает из списка доступных для покупки.
    /// </summary>
    /// <param name="machineId">Идентификатор майнинг-машины</param>
    /// <returns>Результат операции деактивации</returns>
    Task<Result> DeactivateMachineAsync(long machineId);
}
