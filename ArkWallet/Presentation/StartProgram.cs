using ArkWallet.Application.Services;
using ArkWallet.Data;
using ArkWallet.Domain;
using ArkWallet.Domain.Wizard;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Repositories;
using ArkWallet.Telegram;
using Microsoft.EntityFrameworkCore;
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
        services.AddDbContext<ArkWalletDbContext>();
        services.AddScoped<TradingEngine>();
        services.AddScoped<TelegramBot>();
        services.AddScoped<TraderRepository>();
        services.AddScoped<CharacterTokenRepository>();
        services.AddScoped<TradeRepository>();
        services.AddScoped<TradeOrderRepository>();
        services.AddScoped<PortfolioItemRepository>();
        services.AddScoped<QuestionDecorator>();
        services.AddScoped<OrderService>();
        services.AddScoped<UnitOfWork>();

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