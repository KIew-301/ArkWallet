using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.MarketMaker;

/// <summary>
/// Сервис для запросов и обновления ботов-маркетмейкеров
/// </summary>
internal interface IMarketMakerBotQueryService
{
    /// <summary>
    /// Получает всех ботов для указанного символа токена
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <returns>Список ботов</returns>
    Task<Result<List<MarketMakerBot>>> GetBotsBySymbolAsync(string symbol);

    /// <summary>
    /// Получает бота по ID
    /// </summary>
    /// <param name="botId">ID бота</param>
    /// <returns>Данные бота</returns>
    Task<Result<MarketMakerBot>> GetBotByIdAsync(long botId);

    /// <summary>
    /// Обновляет параметры бота (null = оставить текущее значение)
    /// </summary>
    /// <param name="botId">ID бота</param>
    /// <param name="basePower">Новая мощность или null</param>
    /// <param name="role">Новая роль или null</param>
    /// <param name="isActive">Новый флаг активности или null</param>
    /// <returns>Результат операции</returns>
    Task<Result> UpdateBotAsync(long botId, decimal? basePower, string? role, bool? isActive);
}
