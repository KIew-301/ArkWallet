namespace ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;

/// <summary>Уровень существующего ордера на рынке — данные для дедупликации сеток.</summary>
internal sealed record PlacedOrderLevel(decimal Price, bool IsBuy);