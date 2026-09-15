using MediatR;

namespace ArkWallet.Core.GlobalGoalContext.Domain.Events;

internal sealed record GlobalGoalAchievedEvent(
    string GoalName,
    DateTime AchievedAt,
    decimal Target,
    string SymbolForReward,
    decimal AmountForReward
) : INotification;
