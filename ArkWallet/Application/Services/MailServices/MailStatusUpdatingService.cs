using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Domain.Common;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.MailContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MailServices;

/// <summary>
/// Thin scribe for message status transitions. Loads the record, delegates to a single aggregate
/// method that owns the rules and raises domain events, then persists.
/// </summary>
internal class MailStatusUpdatingService(
    ArkWalletDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<MailStatusUpdatingService> logger,
    TimeProvider timeProvider) : IMailStatusUpdatingService
{
    public async Task<Result> MarkAsReadAsync(long mailId, long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var record = await LoadRecordAsync(mailId, traderId);
                if (record is null)
                    return Result.Fail("Письмо не найдено");

                var message = MailContextMapper.ToMessage(record);
                message.SetEventPublisher(eventPublisher);
                message.MarkAsRead(timeProvider.GetUtcNow().UtcDateTime);

                MailContextMapper.ApplyToRecord(record, message);
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
                var record = await LoadRecordAsync(mailId, traderId);
                if (record is null)
                    return Result.Fail("Письмо не найдено");

                var message = MailContextMapper.ToMessage(record);
                message.SetEventPublisher(eventPublisher);
                await message.MarkAsAccepted(timeProvider.GetUtcNow().UtcDateTime);

                MailContextMapper.ApplyToRecord(record, message);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Mail accepted: {MailId} by {TraderId}", mailId, traderId);

                return Result.Ok();
            });
        }, logger, nameof(MailStatusUpdatingService));
    }

    private async Task<MailMessage?> LoadRecordAsync(long mailId, long traderId)
    {
        return await dbContext.MailMessages
            .FirstOrDefaultAsync(m => m.Id == mailId && m.TraderId == traderId);
    }
}
