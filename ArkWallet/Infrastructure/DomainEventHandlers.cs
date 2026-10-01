using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.General.Domain.Exceptions;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.Events;
using ArkWallet.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

        var trader = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == order.TraderId, cancellationToken);
        if (trader?.IsBot == true && order.IsFilled())
        {
            dbContext.TradeOrders.Remove(trackedOrder);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

internal sealed class TradeExecutedEventHandler(ArkWalletDbContext dbContext) : INotificationHandler<TradeExecutedEvent>
{
    public async Task Handle(TradeExecutedEvent notification, CancellationToken cancellationToken)
    {
        var trade = notification.Trade;

        var buyer = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == trade.BuyerId, cancellationToken);
        var seller = await dbContext.Traders.FirstOrDefaultAsync(t => t.Id == trade.SellerId, cancellationToken);
        if (buyer?.IsBot == true && seller?.IsBot == true)
            return;

        dbContext.Trades.Add(TradingContextMapper.ToTrade(trade));
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
