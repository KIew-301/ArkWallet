using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Dtos;
using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.General.Application.Contracts.Other;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MailContext.Domain.Events;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.MailContext.Application.Services.MailServices;

internal class MessageService(
    ArkWalletDbContext dbContext,
    ITaskDispatcher taskDispatcher,
    ILogger<MessageService> logger,
    TimeProvider timeProvider) : IMessageService
{
    public async Task<Result<MailCreateResult>> CreateAsync(CreateCommand command)
    {
        var result = await CreateManyCoreAsync([command]);
        if (!result.TryGetData(out var ids) || ids.Count == 0)
            return Result<MailCreateResult>.Fail(result.Message);

        return Result<MailCreateResult>.Ok(ids[0]);
    }

    public Task<Result<List<MailCreateResult>>> CreateManyAsync(IReadOnlyList<CreateCommand> commands)
        => CreateManyCoreAsync(commands);

    private async Task<Result<List<MailCreateResult>>> CreateManyCoreAsync(IReadOnlyList<CreateCommand> commands)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                if (commands.Count == 0)
                    return Result<List<MailCreateResult>>.Ok(new());

                var createdAt = timeProvider.GetUtcNow().UtcDateTime;

                var mails = commands
                    .Select(c => Mapper.ToRecord(Message.Create(new MessageDraft(
                        c.TraderId,
                        c.Title,
                        c.Message,
                        c.SenderName,
                        c.SenderId,
                        c.SymbolForReward,
                        c.AmountForReward,
                        ParseType(c.Type),
                        createdAt))))
                    .ToList();

                dbContext.MailMessages.AddRange(mails);
                await dbContext.SaveChangesAsync();

                await NotifyAsync(commands);

                var result = mails.Select(m => new MailCreateResult(m.Id)).ToList();

                logger.LogInformation("Mails created: {Count}", result.Count);

                return Result<List<MailCreateResult>>.Ok(result);
            });
        }, logger, nameof(MessageService));
    }

    private static MailType ParseType(string type)
        => Enum.TryParse<MailType>(type, ignoreCase: true, out var parsed) ? parsed : MailType.Notification;

    private async Task NotifyAsync(IReadOnlyList<CreateCommand> commands)
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