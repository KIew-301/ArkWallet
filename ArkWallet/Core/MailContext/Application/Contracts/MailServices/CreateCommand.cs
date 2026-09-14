namespace ArkWallet.Core.MailContext.Application.Contracts.MailServices;

/// <summary>
/// Команда создания письма
/// </summary>
public record CreateCommand(
    long TraderId,
    string Title,
    string Message,
    string SenderName,
    long? SenderId,
    string SymbolForReward,
    decimal AmountForReward,
    string Type = "Notification");
