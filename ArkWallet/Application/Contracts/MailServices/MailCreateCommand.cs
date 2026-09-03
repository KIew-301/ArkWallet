namespace ArkWallet.Application.Contracts.MailServices;

/// <summary>
/// Команда создания письма
/// </summary>
public record MailCreateCommand(
    long TraderId,
    string Title,
    string Message,
    string SenderName,
    long? SenderId,
    string SymbolForReward,
    decimal AmountForReward);
