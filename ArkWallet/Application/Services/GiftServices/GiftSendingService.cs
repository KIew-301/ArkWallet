using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.GiftContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Records = global::ArkWallet.Domain.Entities;

namespace ArkWallet.Application.Services.GiftServices;

internal class GiftSendingService(
    ArkWalletDbContext dbContext,
    ITokenQueryService tokenQueryService,
    IEventPublisher eventPublisher,
    ILogger<GiftSendingService> logger,
    TimeProvider timeProvider) : IGiftSendingService
{
    public async Task<Result<GiftSendResult>> SendGiftAsync(long senderId, long recipientId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockTradersAsync([senderId]);

                var recipientExists = await dbContext.Traders.AnyAsync(t => t.TelegramId == recipientId);
                if (!recipientExists)
                    throw new InvalidOperationException("Получатель не найден");

                var allTokens = await tokenQueryService.GetAllActiveTokensAsync();
                if (!allTokens.TryGetData(out var tokenList))
                    throw new InvalidOperationException("Токены не найдены");

                var tokenPrices = tokenList.ToDictionary(t => t.TokenInfo.Symbol, t => t.TokenInfo.CurrentPrice);

                var portfolioItems = await dbContext.PortfolioItems
                    .Where(p => p.TraderTelegramId == senderId)
                    .ToListAsync();

                var eightHoursAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-8);
                var recentGifts = await dbContext.Gifts
                    .Where(g => g.SenderId == senderId
                        && g.RecipientId == recipientId
                        && g.SentAt > eightHoursAgo)
                    .ToListAsync();

                var sender = await dbContext.Traders.FindAsync(senderId)
                    ?? throw new InvalidOperationException("Отправитель не найден");

                var user = GiftContextMapper.ToUser(
                    sender,
                    recentGifts,
                    new List<Records.Gift>(),
                    portfolioItems);

                user.SetEventPublisher(eventPublisher);

                var sentAt = timeProvider.GetUtcNow().UtcDateTime;

                await user.SendGift(recipientId, tokenPrices, timeProvider);

                SyncGiftState(user, recipientId, sentAt, portfolioItems);

                await dbContext.SaveChangesAsync();

                var lastSent = GetLastSentGift(user);
                if (lastSent is null)
                    throw new InvalidOperationException("Ошибка при отправке подарка");

                logger.LogInformation("Gift sent: {GiftId} from {SenderId} to {RecipientId}: 1 {Symbol}",
                    lastSent.GiftId, senderId, recipientId, lastSent.TokenSymbol);

                return Result<GiftSendResult>.Ok(new GiftSendResult(
                    lastSent.GiftId, senderId, recipientId, lastSent.TokenSymbol, 1, lastSent.PriceAtSend));
            });
        }, logger, nameof(GiftSendingService));
    }

    private void SyncGiftState(User user, long recipientId, DateTime sentAt, List<Records.PortfolioItem> portfolioItems)
    {
        var sentGift = GetLastSentGift(user);
        if (sentGift is null)
            return;

        var matchingItem = portfolioItems.FirstOrDefault(p => p.CharacterTokenId == sentGift.TokenSymbol);
        if (matchingItem is not null)
        {
            matchingItem.RemoveTokens(1, matchingItem.AverageBuyPrice);
            if (matchingItem.Quantity == 0)
                dbContext.PortfolioItems.Remove(matchingItem);
        }

        var giftRecord = Gift.Create(
            sentGift.GiftId,
            user.Id,
            recipientId,
            sentGift.TokenSymbol,
            sentGift.Quantity,
            sentGift.PriceAtSend,
            sentAt);

        dbContext.Gifts.Add(giftRecord);
    }

    private static SentGift? GetLastSentGift(User user)
        => user.GiftsSent.Count > 0 ? user.GiftsSent[user.GiftsSent.Count - 1] : null;
}
