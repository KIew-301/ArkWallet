using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using MediatR;

namespace ArkWallet.Core.TradingContext.Domain.Events;

internal sealed record OrderPlacedEvent(Order Order) : INotification;

internal sealed record OrderFilledEvent(Order Order) : INotification;

internal sealed record TradeExecutedEvent(Trade Trade) : INotification;

internal sealed record TokenPriceUpdatedEvent(Token Token) : INotification;
