using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.TradeOrderServices;

/// <summary>
/// Сервис для получения данных об ордерах трейдера
/// </summary>
public interface IOrderQueryService
{
    /// <summary>
    /// Возвращает список ордеров трейдера с учётом статуса
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="includeActive">Включать активные ордера</param>
    /// <param name="includeFilled">Включать исполненные ордера</param>
    /// <param name="includeCancelled">Включать отменённые ордера</param>
    /// <param name="withTokenInfo">Включать информацию о токене (иконка, текущая цена)</param>
    /// <returns>Список ордеров с основной информацией</returns>
    /// <remarks>
    /// <para>
    /// Фильтрация по статусам:
    /// - Active — активные ордера
    /// - Filled — исполненные
    /// - Cancelled — отменённые
    /// </para>
    /// </remarks>
    Task<Result<List<OrderInfo>>> GetTraderOrdersAsync(
        long traderTelegramId,
        bool includeActive = true,
        bool includeFilled = true,
        bool includeCancelled = true,
        bool withTokenInfo = false);
}

/// <summary>
/// DTO с информацией об ордере для отображения на клиенте
/// </summary>
/// <param name="OrderId">Уникальный идентификатор ордера</param>
/// <param name="Symbol">Символ токена</param>
/// <param name="TokenName">Название токена</param>
/// <param name="Direction">Направление ордера (Buy/Sell)</param>
/// <param name="TotalQuantity">Общее количество токенов в ордере</param>
/// <param name="FilledQuantity">Исполненное количество токенов</param>
/// <param name="FillPercent">Процент исполнения (0-100)</param>
/// <param name="Price">Цена за токен</param>
/// <param name="Status">Статус ордера (Active/Filled/Cancelled)</param>
/// <param name="IconUrl">Ссылка на иконку токена</param>
/// <param name="CurrentPrice">Текущая рыночная цена токена</param>
public record OrderInfo(
    string OrderId,
    string Symbol,
    string TokenName,
    string Direction,
    decimal TotalQuantity,
    decimal FilledQuantity,
    decimal FillPercent,
    decimal Price,
    string Status,
    string? IconUrl = null,
    decimal? CurrentPrice = null
)
{
    internal static OrderInfo FromEntity(TradeOrder order, CharacterToken token, bool withTokenInfo)
    {
        var fillPercent = order.Quantity > 0
            ? (decimal)order.FilledQuantity / order.Quantity * 100m
            : 0m;

        return new OrderInfo(
            order.Id,
            token.Symbol,
            token.Name,
            order.Type == OrderType.Buy ? "Buy" : "Sell",
            order.Quantity,
            order.FilledQuantity,
            fillPercent,
            order.Price,
            order.Status.ToString(),
            withTokenInfo ? token.IconUrl : null,
            withTokenInfo ? token.CurrentPrice : null
        );
    }
}