namespace ArkWallet.Application.Contracts.Other;

/// <summary>
/// Сервис для отправки сообщений пользователям
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// Отправляет сообщение в указанный чат
    /// </summary>
    /// <param name="chatId">ID чата в Telegram</param>
    /// <param name="message">Текст сообщения</param>
    Task SendMessageAsync(long chatId, string message);
}
