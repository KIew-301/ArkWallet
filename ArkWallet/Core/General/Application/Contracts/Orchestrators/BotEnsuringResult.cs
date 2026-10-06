namespace ArkWallet.Core.General.Application.Contracts.Orchestrators;

/// <summary>
/// Результат обеспечения целостности состава ботов: сколько создано, переселено, нормализовано сил.
/// Changed = true, если состав менялся (нужно немедленное обновление сеток и сил).
/// </summary>
public sealed record BotEnsuringResult(
    bool Changed,
    int BotsAdded,
    int BotsMoved,
    int BotsPowerNormalized = 0,
    int BotsOrphanRemoved = 0,
    int DuplicatesRemoved = 0);
