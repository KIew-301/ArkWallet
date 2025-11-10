using ArkWallet.Data;
using ArkWallet.Demo;
using ArkWallet.Domain;
using ArkWallet.Domain.Wizard;
using ArkWallet.Entities;
using ArkWallet.Repositories;
using ArkWallet.Telegram;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<WizardConfiguration>();
        services.AddScoped<WizardEngine>();
        services.AddScoped<ArkWalletDbContext>();
        services.AddScoped<TradingEngine>();
        services.AddScoped<TelegramBot>();
        services.AddScoped<TraderRepository>();
        services.AddScoped<CharacterTokenRepository>();
        services.AddScoped<TradeRepository>();
        services.AddScoped<TradeOrderRepository>();
        services.AddScoped<PortfolioItemRepository>();

        var serviceProvider = services.BuildServiceProvider();

        var bot = serviceProvider.GetRequiredService<TelegramBot>();
        await bot.Start();

        Console.ReadLine();
    }
}