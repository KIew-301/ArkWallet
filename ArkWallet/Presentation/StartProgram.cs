using ArkWallet.Application.Services;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ArkWallet.Entities.Configurations;
using ArkWallet.Domain.Engines;
using ArkWallet.Application.Contracts;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Services;
using ArkWallet.Infrastructure.Services.Wizard;
using ArkWallet.Infrastructure.Repositories;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Main
        services.AddDbContext<ArkWalletDbContext>();
        services.AddScoped<TradingEngine>();
        services.AddScoped<TelegramBot>();

        // Wizard
        services.AddScoped<WizardConfiguration>();
        services.AddScoped<WizardEngine>();
        services.AddScoped<QuestionDecorator>();
        services.AddScoped<KeywordDecorator>();

        // ApplicationServices
        services.AddScoped<CancelOrderService>();
        services.AddScoped<PlaceOrderService>();

        // Repositories
        services.AddScoped<ITraderRepository, TraderRepository>();
        services.AddScoped<ICharacterTokenRepository, CharacterTokenRepository>();
        services.AddScoped<IPortfolioItemRepository, PortfolioItemRepository>();
        services.AddScoped<ITradeOrderRepository, TradeOrderRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();

        // UnitOfWork
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