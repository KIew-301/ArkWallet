using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис для создания майнинг-машин
/// </summary>
public interface IMiningMachineCreationService
{
    /// <summary>
    /// Создаёт одну майнинг-машину
    /// </summary>
    /// <returns>Результат с идентификатором созданной машины</returns>
    Task<Result<MiningMachineCreationData>> CreateMachineAsync(MiningMachineCreationCommand command);

    /// <summary>
    /// Создаёт несколько майнинг-машин пакетно
    /// </summary>
    /// <returns>Результат со списком созданных машин</returns>
    Task<Result<List<MiningMachineCreationData>>> CreateMachinesAsync(IEnumerable<MiningMachineCreationCommand> commands);
}

/// <summary>
/// Команда создания майнинг-машины с её правилами
/// </summary>
/// <param name="Name">Название машины</param>
/// <param name="Type">Тип машины (SMAI, MGC, BMP)</param>
/// <param name="SwitchingTime">Время переключения в минутах</param>
/// <param name="Reusability">Процент возврата от цены покупки</param>
/// <param name="IsActiveForSale">Доступна ли для покупки</param>
/// <param name="Cost">Цена покупки</param>
/// <param name="Image">Ссылка на изображение</param>
/// <param name="Rules">Правила майнинга для машины</param>
public record MiningMachineCreationCommand(
    string Name,
    string Type,
    int SwitchingTime,
    decimal Reusability,
    bool IsActiveForSale,
    decimal Cost,
    string Image,
    List<MiningMachineRuleCreationCommand>? Rules = null);

/// <summary>
/// Результат создания майнинг-машины
/// </summary>
public record MiningMachineCreationData(long Id, string Name);
