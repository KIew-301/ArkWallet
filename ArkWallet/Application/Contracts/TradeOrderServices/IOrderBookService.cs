using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices;

/// <summary>
/// Сервис для построения стакана ордеров (Order Book) по токену
/// </summary>
public interface IOrderBookService
{
    /// <summary>
    /// Получает стакан ордеров для указанного токена
    /// </summary>
    /// <param name="symbol">Символ токена</param>
    /// <param name="buyOrdersCount">Количество ордеров на покупку</param>
    /// <param name="sellOrdersCount">Количество ордеров на продажу</param>
    /// <returns>Результат с данными стакана ордеров</returns>
    Task<Result<OrderBookResult>> GetOrderBookAsync(string symbol, int buyOrdersCount, int sellOrdersCount);
}

/// <summary>
/// Результат построения стакана ордеров
/// </summary>
/// <param name="Symbol">Символ токена</param>
/// <param name="BestBid">Лучшая цена покупки</param>
/// <param name="BestAsk">Лучшая цена продажи</param>
/// <param name="Spread">Спред между лучшими ценами</param>
/// <param name="Bids">Список ордеров на покупку</param>
/// <param name="Asks">Список ордеров на продажу</param>
public record OrderBookResult(
    string Symbol,
    decimal BestBid,
    decimal BestAsk,
    decimal Spread,
    List<OrderBookEntry> Bids,
    List<OrderBookEntry> Asks
);

/// <summary>
/// Запись стакана ордеров
/// </summary>
/// <param name="Side">Сторона ордера (Buy/Sell)</param>
/// <param name="Price">Цена ордера</param>
/// <param name="Quantity">Количество токенов</param>
/// <param name="TotalCost">Общая стоимость ордера</param>
public record OrderBookEntry(
    string Side,
    decimal Price,
    int Quantity,
    decimal TotalCost
);
