using ArkWallet.Domain.GiftContext;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.GiftServices;

/// <summary>
/// Маппинг между записями БД и агрегатом User контекста подарков.
/// </summary>
internal static class GiftContextMapper
{
    internal static User ToUser(
        Records.Trader trader,
        List<Records.Gift> sentGifts,
        List<Records.Gift> receivedGifts,
        List<Records.PortfolioItem> portfolioItems)
    {
        var portfolio = portfolioItems
            .Select(p => new PortfolioPosition(p.CharacterTokenId, p.Quantity))
            .ToList();

        var giftsSent = sentGifts
            .Select(g => new SentGift(
                g.Id,
                g.RecipientId,
                g.TokenSymbol,
                g.Quantity,
                g.PriceAtSend,
                g.SentAt))
            .ToList();

        var giftsReceived = receivedGifts
            .Select(g => new ReceivedGift(
                g.Id,
                g.SenderId,
                g.TokenSymbol,
                g.Quantity,
                g.SentAt))
            .ToList();

        return User.Load(trader.TelegramId, portfolio, giftsSent, giftsReceived);
    }

    internal static Records.Gift ToRecord(
        Guid giftId,
        long senderId,
        long recipientId,
        string tokenSymbol,
        decimal quantity,
        decimal priceAtSend,
        DateTime sentAt)
    {
        return Records.Gift.Create(giftId, senderId, recipientId, tokenSymbol, quantity, priceAtSend, sentAt);
    }
}
