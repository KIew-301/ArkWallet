namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Доменная команда ордера, спланированная ботом (движок сетки, рыночный движок или модификатор).
/// На границе слоя маппится в Application-команду <c>CreateOrderCommand</c>.
/// </summary>
/// <param name="TraderId">ID трейдера в Telegram</param>
/// <param name="Direction">Направление сделки ("купить" или "продать")</param>
/// <param name="Symbol">Символ токена</param>
/// <param name="Quantity">Количество токенов</param>
/// <param name="Price">Цена за токен</param>
public record CreateMarketOrderCommand(
    long TraderId,
    string Direction,
    string Symbol,
    int Quantity,
    decimal Price);