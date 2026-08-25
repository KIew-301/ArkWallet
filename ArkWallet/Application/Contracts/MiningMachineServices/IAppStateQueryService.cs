using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис чтения служебного состояния приложения (AppState)
/// </summary>
public interface IAppStateQueryService
{
    /// <summary>
    /// Возвращает все записи служебного состояния приложения
    /// </summary>
    /// <returns>Результат со списком записей AppState</returns>
    Task<Result<List<AppStateData>>> TakeAllAsync();
}

/// <summary>
/// Запись служебного состояния приложения
/// </summary>
/// <param name="Key">Ключ записи</param>
/// <param name="Value">Значение записи (сериализованный JSON)</param>
public record AppStateData(string Key, string Value);
