using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MailContext.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.MailContext.Application.Services.MailServices;

/// <summary>
/// Thin scribe for message status transitions. Loads the record, delegates to a single aggregate
/// method that owns the rules and raises domain events, then persists.
/// </summary>
internal class StatusUpdatingService(
    ArkWalletDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<StatusUpdatingService> logger,
    TimeProvider timeProvider) : IStatusUpdatingService
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

                var message = Mapper.ToMessage(record);
                message.SetEventPublisher(eventPublisher);
                message.MarkAsRead(timeProvider.GetUtcNow().UtcDateTime);

                Mapper.ApplyToRecord(record, message);
                await dbContext.SaveChangesAsync();

                return Result.Ok();
            });
        }, logger, nameof(StatusUpdatingService));
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

                var message = Mapper.ToMessage(record);
                message.SetEventPublisher(eventPublisher);
                await message.MarkAsAccepted(timeProvider.GetUtcNow().UtcDateTime);

                Mapper.ApplyToRecord(record, message);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Mail accepted: {MailId} by {TraderId}", mailId, traderId);

                return Result.Ok();
            });
        }, logger, nameof(StatusUpdatingService));
    }

    private async Task<MailMessage?> LoadRecordAsync(long mailId, long traderId)
    {
        return await dbContext.MailMessages
            .FirstOrDefaultAsync(m => m.Id == mailId && m.TraderId == traderId);
    }
}
