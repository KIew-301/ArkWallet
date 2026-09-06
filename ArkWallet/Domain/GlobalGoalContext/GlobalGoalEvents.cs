using MediatR;

namespace ArkWallet.Domain.GlobalGoalContext;

internal sealed record GlobalGoalAchievedEvent(
    string GoalName,
    DateTime AchievedAt,
    decimal Target,
    string SymbolForReward,
    decimal AmountForReward
) : INotification;
