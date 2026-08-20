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
    /// <param name="botId">ID бота</param>
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
    Task<Result> ExecuteMarketOrderAsync(long botId);

    /// <summary>
    /// Пакетно исполняет рыночные ордера для всех указанных ботов
    /// </summary>
    /// <param name="botIds">ID ботов, для которых нужно исполнить рыночные ордера</param>
    /// <returns>Результат операции</returns>
    /// <remarks>
    /// <para>
    /// За один вызов формируются и исполняются ордера сразу для всех ботов.
    /// Неактивные и ненайденные боты пропускаются.
    /// </para>
    /// </remarks>
    Task<Result> ExecuteMarketMakerOrdersAsync(IEnumerable<long> botIds);
}
