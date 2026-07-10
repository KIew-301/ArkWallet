using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.MarketMaker;

/// <summary>
/// Сервис для исполнения рыночных ордеров маркет-мейкером
/// </summary>
public interface IMarketMakerOrderService
{
    /// <summary>
    /// Исполняет рыночный ордер для бота на основе его роли и мощности
    /// </summary>
    /// <param name="traderIdInBot">ID трейдера, связанного с ботом</param>
    /// <param name="symbolInBot">Символ токена</param>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// Логика выбора цены:
    /// - Если бот покупатель → покупает на 20% выше текущей цены
    /// - Если бот продавец → продаёт на 20% ниже текущей цены
    /// </para>
    /// <para>
    /// Объём ордера рассчитывается как 30% от базовой мощности бота (минимум 1)
    /// </para>
    /// </remarks>
    Task<Result> ExecuteMarketOrderAsync(long traderIdInBot, string symbolInBot);
}