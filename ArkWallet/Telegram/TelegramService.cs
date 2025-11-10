namespace ArkWallet.Telegram
{
    internal class TelegramService(string message, long chatId)
    {
        public long ChatId { set; get; } = chatId;
        public string ProcessingMessage { set; get; } = message;
    }
}
