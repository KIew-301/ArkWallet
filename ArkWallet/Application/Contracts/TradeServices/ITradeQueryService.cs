using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;

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
/// <param name="TraderRole">Роль трейдера в сделке (Buyer/Seller)</param>
/// <param name="ExecutionPrice">Цена исполнения сделки</param>
/// <param name="Quantity">Количество токенов в сделке</param>
/// <param name="Profit">Прибыль/убыток. Положительное число для продавца, отрицательное для покупателя</param>
/// <param name="TradeDateTime">Дата и время совершения сделки (UTC)</param>
/// <param name="TokenInfo">Информация о токене (символ, название, иконка и др.)</param>
public record TradeInfo(
    string TraderRole,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Profit,
    DateTime TradeDateTime,
    TokenInfo? TokenInfo
);