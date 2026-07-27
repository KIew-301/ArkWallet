using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.Other;

/// <summary>
/// Сервис для расчёта объёма торгов
/// </summary>
public interface ITradingVolumeService
{
    /// <summary>
    /// Объём торгов по конкретному токену за период
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="periodDays">Период в днях (0 = всё время)</param>
    /// <param name="includeBots">Учитывать сделки ботов (ID 100-1000)</param>
    /// <returns>Суммарный объём в рублях</returns>
    Task<Result<decimal>> GetTokenVolumeAsync(string symbol, int periodDays, bool includeBots);

    /// <summary>
    /// Общий объём торгов по всем токенам за период
    /// </summary>
    /// <param name="periodDays">Период в днях (0 = всё время)</param>
    /// <param name="includeBots">Учитывать сделки ботов (ID 100-1000)</param>
    /// <returns>Суммарный объём в рублях</returns>
    Task<Result<decimal>> GetTotalVolumeAsync(int periodDays, bool includeBots);

    /// <summary>
    /// Объём торгов по каждому токену за период
    /// </summary>
    /// <param name="periodDays">Период в днях (0 = всё время)</param>
    /// <param name="includeBots">Учитывать сделки ботов (ID 100-1000)</param>
    /// <returns>Список (symbol, volume)</returns>
    Task<Result<List<(string Symbol, decimal Volume)>>> GetVolumePerTokenAsync(int periodDays, bool includeBots);
}
