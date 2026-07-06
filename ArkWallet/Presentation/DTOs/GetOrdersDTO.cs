using ArkWallet.Application.Contracts.TradeOrderServices;

namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Запрос на получение ордеров с фильтрацией
    /// </summary>
    /// <param name="IncludeActive">Включать активные ордера</param>
    /// <param name="IncludeFilled">Включать исполненные</param>
    /// <param name="IncludeCancelled">Включать отменённые</param>
    public record GetOrdersRequest(bool IncludeActive, bool IncludeFilled, bool IncludeCancelled);
    /// <summary>
    /// Ответ со списком ордеров
    /// </summary>
    /// <param name="Orders">Массив ордеров</param>
    public record GetOrdersResponse(OrderInfo[] Orders);
}
