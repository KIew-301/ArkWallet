namespace ArkWallet.Application.Contracts.Other;

public interface IMessageSender
{
    Task SendMessageAsync(long chatId, string message);
}
