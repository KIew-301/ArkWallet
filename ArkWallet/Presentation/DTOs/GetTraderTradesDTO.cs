using ArkWallet.Core.TradingContext.Application.Contracts.TradeServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ со списком сделок
    /// </summary>
    /// <param name="Trades">Массив сделок</param>
    public record GetTradesResponse(TradeInfo[] Trades);

    /// <summary>
    /// Постраничный ответ со сделками
    /// </summary>
    /// <param name="Items">Сделки текущей страницы</param>
    /// <param name="Page">Номер страницы (с 1)</param>
    /// <param name="PageSize">Размер страницы</param>
    /// <param name="TotalCount">Общее количество сделок</param>
    /// <param name="TotalPages">Общее количество страниц</param>
    /// <param name="HasNext">Есть ли следующая страница</param>
    /// <param name="HasPrevious">Есть ли предыдущая страница</param>
    public record GetTradesPageResponse(
        TradeInfo[] Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasNext,
        bool HasPrevious);
}
