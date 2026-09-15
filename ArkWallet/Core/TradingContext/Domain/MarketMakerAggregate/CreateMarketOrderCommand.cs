namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>
/// Команда создания торгового ордера, формируемая движком сетки в слое Domain.
/// </summary>
/// <param name="TraderId">ID трейдера в Telegram</param>
/// <param name="Direction">Направление сделки ("купить" или "продать")</param>
/// <param name="Symbol">Символ токена</param>
/// <param name="Quantity">Количество токенов</param>
/// <param name="Price">Цена за токен</param>
internal record CreateMarketOrderCommand(
    long TraderId,
    string Direction,
    string Symbol,
    int Quantity,
    decimal Price);