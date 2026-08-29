using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.GiftServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.GiftServices;

internal class GiftQueryService(ArkWalletDbContext dbContext, ILogger<GiftQueryService> logger) : IQueryGiftService
{
    public async Task<Result<List<GiftInfo>>> GetPendingGiftsAsync(long recipientId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var gifts = await dbContext.Gifts
                .Where(g => g.RecipientId == recipientId && g.Status == "Sent")
                .OrderBy(g => g.SentAt)
                .ThenBy(g => g.Id)
                .Select(g => new GiftInfo(g.Id, g.SenderId, g.TokenSymbol, g.Quantity, g.SentAt))
                .ToListAsync();

            return Result<List<GiftInfo>>.Ok(gifts);
        }, logger, nameof(GiftQueryService));
    }
}
