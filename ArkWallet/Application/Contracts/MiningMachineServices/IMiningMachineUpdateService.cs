using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис для изменения майнинг-машин. Имя и стоимость пересчитываются автоматически.
/// </summary>
public interface IMiningMachineUpdateService
{
    /// <summary>
    /// Обновляет переданные поля майнинг-машины
    /// </summary>
    /// <returns>Результат операции</returns>
    Task<Result> UpdateMachineAsync(MiningMachineUpdateCommand command);
}

/// <summary>
/// Команда изменения майнинг-машины. Null-поля не изменяются.
/// </summary>
/// <param name="MachineId">Идентификатор машины</param>
/// <param name="Type">Тип машины (SMAI, MGC, BMP)</param>
/// <param name="SwitchingTime">Время переключения в минутах</param>
/// <param name="Reusability">Процент возврата от цены покупки</param>
/// <param name="IsActiveForSale">Доступна ли для покупки</param>
/// <param name="Image">Ссылка на изображение</param>
/// <param name="Efficiency">Коэффициент производительности машины</param>
public record MiningMachineUpdateCommand(
    long MachineId,
    string? Type = null,
    int? SwitchingTime = null,
    decimal? Reusability = null,
    bool? IsActiveForSale = null,
    string? Image = null,
    decimal? Efficiency = null);
