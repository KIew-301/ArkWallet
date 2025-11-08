using ArkWallet.Telegram;

class Program
{
    // Входная точка в программу
    static async Task Main(string[] args)
    {
        _ = new TelegramBot();
        Console.ReadLine();
    }
}