using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.GiftContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

                var sender = await dbContext.Traders.FindAsync(senderId)
                    ?? throw new InvalidOperationException("Отправитель не найден");

                var recipientExists = await dbContext.Traders.AnyAsync(t => t.TelegramId == recipientId);
                if (!recipientExists)
                    return Result<GiftSendResult>.Fail("Получатель не найден");

                var allTokens = await tokenQueryService.GetAllActiveTokensAsync();
                if (!allTokens.TryGetData(out var tokenList))
                    return Result<GiftSendResult>.Fail("Токены не найдены");

                var tokenPrices = tokenList.ToDictionary(t => t.TokenInfo.Symbol, t => t.TokenInfo.CurrentPrice);

                var cache = await LoadContextAsync(senderId, recipientId, tokenPrices);
                var createdAt = timeProvider.GetUtcNow().UtcDateTime;

                var user = User.Load(
                    senderId,
                    sender.Username ?? $"ID {senderId}",
                    cache.Portfolio.Select(p => GiftContextMapper.ToTokens(p.Item, p.Price)).ToList(),
                    cache.SentGifts);

                user.SetEventPublisher(eventPublisher);

                var giftSent = await user.SendGift(recipientId, createdAt);

                foreach (var entry in cache.Portfolio)
                {
                    var record = entry.Item;
                    var token = user.Portfolio.FirstOrDefault(t => t.Symbol == record.CharacterTokenId);

                    if (token is null)
                        dbContext.PortfolioItems.Remove(record);
                    else
                        GiftContextMapper.ApplyToRecord(record, token);
                }

                await dbContext.SaveChangesAsync();

                logger.LogInformation("Gift sent: from {SenderId} to {RecipientId}: 1 {Symbol}",
                    senderId, recipientId, giftSent.Symbol);

                return Result<GiftSendResult>.Ok(new GiftSendResult(
                    senderId, recipientId, giftSent.Symbol, giftSent.Quantity));
            });
        }, logger, nameof(GiftSendingService));
    }

    private async Task<GiftContextData> LoadContextAsync(
        long senderId,
        long recipientId,
        IReadOnlyDictionary<string, decimal> tokenPrices)
    {
        var portfolioItems = await dbContext.PortfolioItems
            .Where(p => p.TraderTelegramId == senderId)
            .ToListAsync();

        var portfolio = portfolioItems
            .Where(p => tokenPrices.TryGetValue(p.CharacterTokenId, out var _))
            .Select(p => new TokenWithPrice(p, tokenPrices[p.CharacterTokenId]))
            .ToList();

        var eightHoursAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-8);
        var sentGifts = await dbContext.MailMessages
            .Where(m => m.TraderId == recipientId
                && m.SenderId == senderId
                && m.Type == MailType.Gift.ToString()
                && m.CreatedAt > eightHoursAgo)
            .Select(m => GiftContextMapper.ToSentGift(m))
            .ToListAsync();

        return new GiftContextData(portfolio, sentGifts);
    }

    private sealed record TokenWithPrice(
        ArkWallet.Domain.Entities.PortfolioItem Item,
        decimal Price);

    private sealed record GiftContextData(
        List<TokenWithPrice> Portfolio,
        List<SentGift> SentGifts);
}
