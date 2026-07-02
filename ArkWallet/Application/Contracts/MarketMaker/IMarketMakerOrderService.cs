using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.MarketMaker;

/// <summary>
/// Сервис для исполнения рыночных ордеров маркет-мейкером
/// </summary>
internal interface IMarketMakerOrderService
{
    /// <summary>
    /// Исполняет рыночный ордер для бота на основе его роли и мощности
    /// </summary>
    /// <param name="bot">Бот-маркетмейкер</param>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// Логика выбора цены:
    /// - Если бот покупатель → берёт самую высокую цену среди активных ордеров на продажу
    /// - Если бот продавец → берёт самую низкую цену среди активных ордеров на покупку
    /// </para>
    /// <para>
    /// Объём ордера рассчитывается как 30% от базовой мощности бота (минимум 1)
    /// </para>
    /// </remarks>
    Task<Result> ExecuteMarketOrderAsync(MarketMakerBot bot);
}