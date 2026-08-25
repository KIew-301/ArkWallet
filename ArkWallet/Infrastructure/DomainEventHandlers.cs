using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Domain.Exceptions;
using ArkWallet.Domain.TradingContext;
using ArkWallet.Infrastructure.Data;
using MediatR;

namespace ArkWallet.Infrastructure;

internal sealed class OrderPlacedEventHandler(ArkWalletDbContext dbContext) : INotificationHandler<OrderPlacedEvent>
{
    public Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
    {
        dbContext.TradeOrders.Add(TradingContextMapper.ToRecord(notification.Order));
        return Task.CompletedTask;
    }
}

internal sealed class OrderFilledEventHandler(ArkWalletDbContext dbContext) : INotificationHandler<OrderFilledEvent>
{
    public async Task Handle(OrderFilledEvent notification, CancellationToken cancellationToken)
    {
        var order = notification.Order;
        var trackedOrder = dbContext.TradeOrders.Local.FirstOrDefault(o => o.Id == order.Id);
        if (trackedOrder == null)
            return;

        TradingContextMapper.ApplyTo(trackedOrder, order);

        if (BotFilter.IsBot(order.TraderId) && order.IsFilled())
        {
            dbContext.TradeOrders.Remove(trackedOrder);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

internal sealed class TradeExecutedEventHandler(ArkWalletDbContext dbContext) : INotificationHandler<TradeExecutedEvent>
{
    public Task Handle(TradeExecutedEvent notification, CancellationToken cancellationToken)
    {
        var trade = notification.Trade;

        if (BotFilter.IsBotBotTrade(trade.BuyerId, trade.SellerId))
            return Task.CompletedTask;

        dbContext.Trades.Add(TradingContextMapper.ToTrade(trade));
        return Task.CompletedTask;
    }
}

internal sealed class TokenPriceUpdatedEventHandler(
    ArkWalletDbContext dbContext,
    ITokenPriceCandleUpdateService tokenPriceCandleUpdateService) : INotificationHandler<TokenPriceUpdatedEvent>
{
    public async Task Handle(TokenPriceUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var trackedToken = dbContext.CharacterTokens.Local
            .FirstOrDefault(t => t.Symbol == notification.Token.Symbol);

        if (trackedToken != null)
            TradingContextMapper.ApplyTo(trackedToken, notification.Token);

        var result = await tokenPriceCandleUpdateService
            .UpdateTokenPriceCandleAsync(notification.Token.Symbol, notification.Token.CurrentPrice);

        if (!result.IsSuccess)
            throw new DomainException(result.Message);
    }
}
