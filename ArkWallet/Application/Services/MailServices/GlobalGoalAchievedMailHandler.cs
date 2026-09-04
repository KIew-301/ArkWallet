using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MailServices;
using ArkWallet.Domain.Entities;
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
        var allTraderIds = await dbContext.Traders
            .Select(t => t.TelegramId)
            .ToListAsync(cancellationToken);

        var traderIds = allTraderIds.Where(id => !BotFilter.IsBot(id)).ToList();

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
                notification.AmountForReward,
                Type: MailType.Reward.ToString()))
            .ToList();

        await mailMessageService.CreateManyAsync(commands);
    }
}
