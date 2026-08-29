using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.GiftContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.GiftServices;

internal class GiftReceivingService(
    ArkWalletDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<GiftReceivingService> logger,
    TimeProvider timeProvider) : IGiftReceivingService
{
    public async Task<Result<GiftReceiveResult>> ReceiveGiftAsync(long recipientId, Guid giftId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([recipientId]);

                var context = await PrepareReceiveContextAsync(recipientId, giftId);

                await context.Recipient.ReceiveGift(
                    context.Gift.Id,
                    context.Gift.SenderId,
                    context.Gift.TokenSymbol,
                    context.Gift.Quantity,
                    timeProvider);

                SyncReceiveState(context);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Gift received: {GiftId} by {RecipientId}", giftId, recipientId);

                return Result<GiftReceiveResult>.Ok(new GiftReceiveResult(
                    context.Gift.Id,
                    context.Gift.SenderId,
                    recipientId,
                    context.Gift.TokenSymbol,
                    context.Gift.Quantity));
            });
        }, logger, nameof(GiftReceivingService));
    }

    public async Task<Result<GiftReceiveAllResult>> ReceiveAllGiftsAsync(long recipientId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([recipientId]);

                var context = await PrepareReceiveAllContextAsync(recipientId);

                await context.Recipient.ReceiveAllGifts(timeProvider);

                var gifts = context.PendingGifts
                    .Select(g => new GiftReceiveResult(g.Id, g.SenderId, recipientId, g.TokenSymbol, g.Quantity))
                    .ToList();

                SyncReceiveAllState(context);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("All gifts received: {Count} gifts by {RecipientId}", gifts.Count, recipientId);

                return Result<GiftReceiveAllResult>.Ok(new GiftReceiveAllResult(
                    recipientId, gifts.Count, gifts));
            });
        }, logger, nameof(GiftReceivingService));
    }

    private async Task<ReceiveContext> PrepareReceiveContextAsync(long recipientId, Guid giftId)
    {
        var gift = await dbContext.Gifts.FindAsync(giftId)
            ?? throw new InvalidOperationException("Подарок не найден");

        if (gift.Status == "Received")
            throw new InvalidOperationException("Подарок уже принят");

        var recipient = await dbContext.Traders.FindAsync(recipientId)
            ?? throw new InvalidOperationException("Получатель не найден");

        var portfolioItems = await dbContext.PortfolioItems
            .Where(p => p.TraderTelegramId == recipientId)
            .ToListAsync();

        var user = GiftContextMapper.ToUser(
            recipient,
            new List<Records.Gift>(),
            new List<Records.Gift> { gift },
            portfolioItems);

        user.SetEventPublisher(eventPublisher);

        return new ReceiveContext
        {
            Recipient = user,
            Gift = gift,
            PortfolioItems = portfolioItems
        };
    }

    private async Task<ReceiveAllContext> PrepareReceiveAllContextAsync(long recipientId)
    {
        var pendingGifts = await dbContext.Gifts
            .Where(g => g.RecipientId == recipientId && g.Status == "Sent")
            .ToListAsync();

        if (pendingGifts.Count == 0)
            throw new InvalidOperationException("Нет подарков для получения");

        var recipient = await dbContext.Traders.FindAsync(recipientId)
            ?? throw new InvalidOperationException("Получатель не найден");

        var portfolioItems = await dbContext.PortfolioItems
            .Where(p => p.TraderTelegramId == recipientId)
            .ToListAsync();

        var user = GiftContextMapper.ToUser(
            recipient,
            new List<Records.Gift>(),
            pendingGifts,
            portfolioItems);

        user.SetEventPublisher(eventPublisher);

        return new ReceiveAllContext
        {
            Recipient = user,
            PendingGifts = pendingGifts,
            PortfolioItems = portfolioItems
        };
    }

    private void SyncReceiveState(ReceiveContext context)
    {
        var gift = context.Gift;
        gift.MarkAsReceived(timeProvider.GetUtcNow().UtcDateTime);

        var receivedGift = context.Recipient.GiftsReceived
            .FirstOrDefault(g => g.GiftId == gift.Id);

        if (receivedGift is not null)
        {
            var existing = context.PortfolioItems
                .FirstOrDefault(p => p.CharacterTokenId == receivedGift.TokenSymbol);

            if (existing is null)
            {
                var newPortfolioItem = PortfolioItem.Create(
                    gift.RecipientId,
                    receivedGift.TokenSymbol,
                    (int)receivedGift.Quantity,
                    gift.PriceAtSend);
                dbContext.PortfolioItems.Add(newPortfolioItem);
            }
            else
            {
                existing.BuyTokens((int)receivedGift.Quantity, gift.PriceAtSend);
            }
        }
    }

    private void SyncReceiveAllState(ReceiveAllContext context)
    {
        var receivedAt = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var gift in context.PendingGifts)
        {
            gift.MarkAsReceived(receivedAt);

            var existing = context.PortfolioItems
                .FirstOrDefault(p => p.CharacterTokenId == gift.TokenSymbol);

            if (existing is null)
            {
                var newPortfolioItem = PortfolioItem.Create(
                    gift.RecipientId,
                    gift.TokenSymbol,
                    (int)gift.Quantity,
                    gift.PriceAtSend);
                dbContext.PortfolioItems.Add(newPortfolioItem);
            }
            else
            {
                existing.BuyTokens((int)gift.Quantity, gift.PriceAtSend);
            }
        }
    }

    private class ReceiveContext
    {
        public User Recipient { get; init; } = null!;
        public Records.Gift Gift { get; init; } = null!;
        public List<Records.PortfolioItem> PortfolioItems { get; init; } = new();
    }

    private class ReceiveAllContext
    {
        public User Recipient { get; init; } = null!;
        public List<Records.Gift> PendingGifts { get; init; } = new();
        public List<Records.PortfolioItem> PortfolioItems { get; init; } = new();
    }
}
