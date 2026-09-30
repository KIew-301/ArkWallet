namespace ArkWallet.Core.General.Application.Contracts.Orchestrators;

/// <summary>
/// Результат обеспечения целостности состава ботов: сколько создано, сколько переселено.
/// Changed = true, если состав менялся (нужно немедленное обновление сеток и сил).
/// </summary>
public sealed record BotEnsuringResult(bool Changed, int BotsAdded, int BotsMoved);
