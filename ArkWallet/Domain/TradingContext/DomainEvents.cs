using MediatR;

namespace ArkWallet.Domain.TradingContext;

internal sealed record OrderPlacedEvent(Order Order) : INotification;

internal sealed record OrderFilledEvent(Order Order) : INotification;

internal sealed record TradeExecutedEvent(Trade Trade) : INotification;

internal sealed record TokenPriceUpdatedEvent(Token Token) : INotification;
