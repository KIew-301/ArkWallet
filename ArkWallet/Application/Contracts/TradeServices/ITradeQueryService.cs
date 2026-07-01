using ArkWallet.Application.Common;
using ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Contracts.TradeServices;

/// <summary>
/// Сервис для получения данных о сделках трейдера
/// </summary>
public interface ITradeQueryService
{
    /// <summary>
    /// Возвращает список сделок трейдера
    /// </summary>
    /// <param name="traderTelegramId">Telegram ID трейдера</param>
    /// <param name="withTokenInfo">Включать информацию о токене (иконка)</param>
    /// <returns>Список сделок с основной информацией</returns>
    /// <remarks>
    /// <para>
    /// Возвращает все сделки трейдера, где он выступал как покупатель или продавец.
    /// </para>
    /// <para>
    /// Profit рассчитывается:
    /// - Для продавца: положительное число (получил деньги)
    /// - Для покупателя: отрицательное число (потратил деньги)
    /// </para>
    /// </remarks>
    Task<Result<List<TradeInfo>>> GetTraderTradesAsync(long traderTelegramId, bool withTokenInfo = false);
}

/// <summary>
/// DTO с информацией о сделке для отображения на клиенте
/// </summary>
/// <param name="Symbol">Символ токена</param>
/// <param name="TraderRole">Роль трейдера в сделке (Buyer/Seller)</param>
/// <param name="ExecutionPrice">Цена исполнения</param>
/// <param name="Quantity">Количество токенов</param>
/// <param name="Profit">Прибыль/убыток (положительное для продавца, отрицательное для покупателя)</param>
/// <param name="TradeDateTime">Дата и время сделки</param>
/// <param name="TokenIconUrl">Ссылка на иконку токена</param>
public record TradeInfo(
    string Symbol,
    string TraderRole,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Profit,
    DateTime TradeDateTime,
    string? TokenIconUrl = null
)
{
    internal static TradeInfo FromEntity(Trade trade, bool withTokenInfo)
    {
        return new TradeInfo(
            trade.CharacterTokenId,
            "", // TraderRole будет установлен отдельно
            trade.Price,
            trade.Quantity,
            0, // Profit будет рассчитан отдельно
            trade.ExecutedAt,
            withTokenInfo ? trade.CharacterToken?.IconUrl : null
        );
    }
}