using ArkWallet.Telegram;
using ArkWallet.Demo;

class Program
{
    // Входная точка в программу
    static async Task Main(string[] args)
    {
        _ = new TelegramBot();
        await TradingDemo.RunDemo();
        Console.ReadLine();
    }
}