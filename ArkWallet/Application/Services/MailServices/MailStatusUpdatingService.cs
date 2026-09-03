using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MailServices;

internal class MailStatusUpdatingService(
    ArkWalletDbContext dbContext,
    ILogger<MailStatusUpdatingService> logger,
    TimeProvider timeProvider) : IMailStatusUpdatingService
{
    public async Task<Result> MarkAsReadAsync(long mailId, long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var mail = await dbContext.MailMessages
                    .FirstOrDefaultAsync(m => m.Id == mailId && m.TraderId == traderId);

                if (mail is null)
                    return Result.Fail("Письмо не найдено");

                mail.MarkAsRead(timeProvider.GetUtcNow().UtcDateTime);
                await dbContext.SaveChangesAsync();

                return Result.Ok();
            });
        }, logger, nameof(MailStatusUpdatingService));
    }

    public async Task<Result> MarkAsAcceptedAsync(long mailId, long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var mail = await dbContext.MailMessages
                    .FirstOrDefaultAsync(m => m.Id == mailId && m.TraderId == traderId);

                if (mail is null)
                    return Result.Fail("Письмо не найдено");

                mail.MarkAsAccepted(timeProvider.GetUtcNow().UtcDateTime);
                await dbContext.SaveChangesAsync();

                return Result.Ok();
            });
        }, logger, nameof(MailStatusUpdatingService));
    }
}
