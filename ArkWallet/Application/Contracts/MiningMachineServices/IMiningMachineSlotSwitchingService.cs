using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MiningMachineServices;

/// <summary>
/// Сервис переключения слота майнинг-машины на другой токен
/// </summary>
public interface IMiningMachineSlotSwitchingService
{
    /// <summary>
    /// Запускает переключение слота на майнинг другого токена.
    /// Возвращает собранные токены старого токена для возврата в портфель.
    /// </summary>
    /// <param name="traderId">Идентификатор трейдера</param>
    /// <param name="miningMachineSlotId">Идентификатор слота</param>
    /// <param name="symbol">Символ токена (characterTokenId) для переключения</param>
    /// <returns>Результат с символом старого токена и собранными токенами</returns>
    Task<Result<MiningTokenCollectionResult>> SwitchTargetTokenAsync(long traderId, long miningMachineSlotId, string symbol);

    /// <summary>
    /// Завершает переключение всех слотов, у которых истекло время переключения
    /// </summary>
    /// <returns>Результат с количеством завершённых переключений</returns>
    Task<Result<int>> CheckSwitchingAsync();
}

/// <summary>
/// Результат сбора токенов: символ токена и целое количество собранных токенов
/// </summary>
/// <param name="Symbol">Символ токена</param>
/// <param name="TokensCollected">Целое количество собранных токенов</param>
public record MiningTokenCollectionResult(string Symbol, int TokensCollected);
