using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Domain.GlobalGoalContext;
using ArkWallet.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Application.Services.MailServices;

/// <summary>
/// Перехватывает достижение глобальной цели и рассылает письма-награды всем участникам (не ботам)
/// </summary>
internal sealed class GlobalGoalAchievedMailHandler(
    ArkWalletDbContext dbContext,
    IMailMessageService mailMessageService) : INotificationHandler<GlobalGoalAchievedEvent>
{
    public async Task Handle(GlobalGoalAchievedEvent notification, CancellationToken cancellationToken)
    {
        var traderIds = await dbContext.Traders
            .Where(t => !BotFilter.IsBot(t.TelegramId))
            .Select(t => t.TelegramId)
            .ToListAsync(cancellationToken);

        var title = $"🏆 Цель достигнута: {notification.GoalName}";
        var message = $"Поздравляем! Участники вместе достигли цели «{notification.GoalName}» ({notification.Target:F2}).\n" +
                      $"Вам начислена награда: {notification.AmountForReward:F2} {notification.SymbolForReward}.";

        var commands = traderIds
            .Select(traderId => new MailCreateCommand(
                traderId,
                title,
                message,
                SenderName: "Система",
                SenderId: null,
                notification.SymbolForReward,
                notification.AmountForReward))
            .ToList();

        await mailMessageService.CreateManyAsync(commands);
    }
}
