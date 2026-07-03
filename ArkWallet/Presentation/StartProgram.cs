using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.FullValidationService;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Application.Services.Other;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.SuggestionServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Application.Workers;
using ArkWallet.Domain.Engines;
using ArkWallet.Entities.Configurations;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Wizard;
using ArkWallet.Telegram;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddLogging(builder => builder.AddConsole());

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>()
            .Build();

        builder.Services.AddSingleton<IConfiguration>(configuration);

        // Main
        builder.Services.AddDbContext<ArkWalletDbContext>(options =>
            options.UseSqlite("Data Source=arkwallet.db"));

        // Services
        RegisterServices(builder.Services);

        // Background Services
        builder.Services.AddHostedService<MarketMakerWorker>();
        builder.Services.AddHostedService<NotificationWorker>();

        var serviceProvider = builder.Services.BuildServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
            await db.Database.MigrateAsync();
            Console.WriteLine("Миграции применены!");
        }

        // Telegram Bot
        var bot = serviceProvider.GetRequiredService<TelegramBot>();
        await bot.Start();

        // Hosted Services
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(CancellationToken.None);

        Console.ReadLine();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // DbContext
        services.AddDbContext<ArkWalletDbContext>();

        // Domain Engines
        services.AddScoped<TradingEngine>();

        // Telegram Bot
        services.AddScoped<TelegramBot>();

        // RabbitMQ
        services.AddScoped<RabbitMQService>();
        services.AddScoped<ITaskDispatcher, RabbitMQTaskDispatcher>();

        // Wizard
        services.AddScoped<WizardConfiguration>();
        services.AddScoped<WizardEngine>();

        // CharacterTokenServices
        services.AddScoped<ITokenCreationService, TokenCreationService>();
        services.AddScoped<ITokenPriceCandleUpdateService, TokenPriceCandleUpdateService>();
        services.AddScoped<ITokenQueryService, TokenQueryService>();
        services.AddScoped<ITokenPriceChangesCalculationService, TokenPriceChangeCalculationService>();

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
        services.AddScoped<IOrderCancellationService, OrderCancellationService>();
        services.AddScoped<IOrderCreationService, OrderCreationService>();
        services.AddScoped<IOrderCreationFullValidationService, OrderCreationFullValidationService>();
        services.AddScoped<IOrderValidationService, OrderValidationService>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();

        // TraderServices
        services.AddScoped<ITraderBalanceUpdatingService, TraderBalanceUpdatingService>();
        services.AddScoped<ITraderRegistrationService, TraderRegistrationService>();
        services.AddScoped<IBalanceSnapshotService, BalanceSnapshotService>();
        services.AddScoped<IBalanceChangesCalculationService, BalanceChangesCalculationService>();

        // MarketMaker
        services.AddScoped<IMarketMakerBotRegistrationService, MarketMakerBotRegistrationService>();
        services.AddScoped<IMarketMakerOrchestrator, MarketMakerOrchestrator>();
        services.AddScoped<IMarketMakerOrderService, MarketMakerOrderService>();

        // Other
        services.AddScoped<ReserveCalculationService>();
        services.AddScoped<ITraderAuthService, TraderAuthService>();
        services.AddScoped<ITokenService, TokenService>();
    }

    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArkWalletDbContext>
    {
        public ArkWalletDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ArkWalletDbContext>();
            optionsBuilder.UseSqlite("Data Source=arkwallet.db");
            return new ArkWalletDbContext(optionsBuilder.Options);
        }
    }
}