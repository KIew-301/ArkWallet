using ArkWallet.Application.Services;
using ArkWallet.Data;
using ArkWallet.Domain;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Repositories;
using ArkWallet.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ArkWallet.Contracts;
using ArkWallet.Entities.Configurations;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<WizardConfiguration>();
        services.AddScoped<WizardEngine>();
        services.AddDbContext<ArkWalletDbContext>();
        services.AddScoped<TradingEngine>();
        services.AddScoped<TelegramBot>();
        services.AddScoped<QuestionDecorator>();
        services.AddScoped<OrderService>();
        services.AddScoped<UnitOfWork>();

        services.AddScoped<ITraderRepository, TraderRepository>();
        services.AddScoped<ICharacterTokenRepository, CharacterTokenRepository>();
        services.AddScoped<IPortfolioItemRepository, PortfolioItemRepository>();
        services.AddScoped<ITradeOrderRepository, TradeOrderRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
            await db.Database.MigrateAsync();
            Console.WriteLine("Миграции применены!");
        }

        var bot = serviceProvider.GetRequiredService<TelegramBot>();
        await bot.Start();

        Console.ReadLine();
    }
}