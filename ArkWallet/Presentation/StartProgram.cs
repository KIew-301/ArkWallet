using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.SuggestionServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Entities.Configurations;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Repositories;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Wizard;
using ArkWallet.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Main
        services.AddDbContext<ArkWalletDbContext>();
        services.AddScoped<TradingEngine>();

        services.AddScoped<TelegramBot>();

        services.AddScoped<RabbitMQService>();
        services.AddScoped<ITaskDispatcher, RabbitMQTaskDispatcher>();

        services.AddHostedService<NotificationWorker>();

        // Wizard
        services.AddScoped<WizardConfiguration>();
        services.AddScoped<WizardEngine>();

        // CharacterTokenServices
        services.AddScoped<ITokenCreationService, TokenCreationService>();
        services.AddScoped<ITokenQueryService, TokenQueryService>();

        // Decorators
        services.AddScoped<IButtonDecorator, ButtonDecorator>();
        services.AddScoped<IQuestionDecorator, QuestionDecorator>();

        // PortfolioServices
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();
        services.AddScoped<IPortfolioUpdatingService, PortfolioUpdatingService>();

        // SuggestionServices
        services.AddScoped<IPriceSuggestionService, PriceSuggestionService>();
        services.AddScoped<IQuantitySuggestionService, QuantitySuggestionService>();

        // TradeOrderServices
        services.AddScoped<IOrderCancelService, OrderCancelService>();
        services.AddScoped<IOrderCreationService, OrderCreationService>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        services.AddScoped<IOrderValidationService, OrderValidationService>();

        // TraderServices
        services.AddScoped<ITraderBalanceUpdatingService, TraderBalanceUpdatingService>();
        services.AddScoped<ITraderQueryService, TraderQueryService>();
        services.AddScoped<ITraderRegistrationService, TraderRegistrationService>();

        // Repositories
        services.AddScoped<ITraderRepository, TraderRepository>();
        services.AddScoped<ICharacterTokenRepository, CharacterTokenRepository>();
        services.AddScoped<IPortfolioItemRepository, PortfolioItemRepository>();
        services.AddScoped<ITradeOrderRepository, TradeOrderRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();

        // UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var serviceProvider = services.BuildServiceProvider();



        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
            await db.Database.MigrateAsync();
            Console.WriteLine("Миграции применены!");
        }

        var bot = serviceProvider.GetRequiredService<TelegramBot>();
        await bot.Start();

        var hostedServices = serviceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(CancellationToken.None);

        Console.ReadLine();
    }
}