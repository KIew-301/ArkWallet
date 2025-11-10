using ArkWallet.Telegram;
using ArkWallet.Demo;

class Program
{
    // Входная точка в программу
    static async Task Main(string[] args)
    {
        _ = new TelegramBot();
        TradingDemo.RunDemo();
        Console.ReadLine();
    }
}