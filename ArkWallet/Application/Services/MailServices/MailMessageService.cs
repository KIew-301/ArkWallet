using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.MailContext;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MailServices;

internal class MailMessageService(
    ArkWalletDbContext dbContext,
    ITaskDispatcher taskDispatcher,
    ILogger<MailMessageService> logger,
    TimeProvider timeProvider) : IMailMessageService
{
    public async Task<Result<MailCreateResult>> CreateAsync(MailCreateCommand command)
    {
        var result = await CreateManyCoreAsync([command]);
        if (!result.TryGetData(out var ids) || ids.Count == 0)
            return Result<MailCreateResult>.Fail(result.Message);

        return Result<MailCreateResult>.Ok(ids[0]);
    }

    public Task<Result<List<MailCreateResult>>> CreateManyAsync(IReadOnlyList<MailCreateCommand> commands)
        => CreateManyCoreAsync(commands);

    private async Task<Result<List<MailCreateResult>>> CreateManyCoreAsync(IReadOnlyList<MailCreateCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                if (commands.Count == 0)
                    return Result<List<MailCreateResult>>.Ok(new());

                var createdAt = timeProvider.GetUtcNow().UtcDateTime;

                var mails = commands
                    .Select(c => MailContextMapper.ToRecord(Message.Create(
                        c.TraderId,
                        c.Title,
                        c.Message,
                        c.SenderName,
                        c.SenderId,
                        c.SymbolForReward,
                        c.AmountForReward,
                        ParseType(c.Type),
                        createdAt)))
                    .ToList();

                dbContext.MailMessages.AddRange(mails);
                await dbContext.SaveChangesAsync();

                await NotifyAsync(commands);

                var result = mails.Select(m => new MailCreateResult(m.Id)).ToList();

                logger.LogInformation("Mails created: {Count}", result.Count);

                return Result<List<MailCreateResult>>.Ok(result);
            });
        }, logger, nameof(MailMessageService));
    }

    private static MailType ParseType(string type)
        => Enum.TryParse<MailType>(type, ignoreCase: true, out var parsed) ? parsed : MailType.Notification;

    private async Task NotifyAsync(IReadOnlyList<MailCreateCommand> commands)
    {
        var traderIds = commands.Select(c => c.TraderId).Distinct().ToList();

        var notificationOnIds = await dbContext.Traders
            .Where(t => traderIds.Contains(t.TelegramId) && t.NotificationOn)
            .Select(t => t.TelegramId)
            .ToListAsync();

        if (notificationOnIds.Count == 0)
            return;

        var notifications = commands
            .Where(c => notificationOnIds.Contains(c.TraderId))
            .Select(c => new NotificationEvent(c.TraderId, $"Новое сообщение, проверьте почту: {c.Title}"))
            .ToList();

        if (notifications.Count == 0)
            return;

        await taskDispatcher.SendTaskAsync("notification", notifications);
    }
}