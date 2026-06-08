using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.Other;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.SuggestionServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
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
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
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

        builder.Services.AddDbContext<ArkWalletDbContext>(options =>
            options.UseSqlite("Data Source=arkwallet.db"));

        builder.Services.AddSingleton<IConfiguration>(configuration);

        // Main
        builder.Services.AddDbContext<ArkWalletDbContext>();
        builder.Services.AddScoped<TradingEngine>();

        builder.Services.AddScoped<TelegramBot>();

        builder.Services.AddScoped<RabbitMQService>();
        builder.Services.AddScoped<ITaskDispatcher, RabbitMQTaskDispatcher>();

        builder.Services.AddHostedService<NotificationWorker>();

        builder.Services.AddScoped<ReserveCalculationService>();

        // Wizard
        builder.Services.AddScoped<WizardConfiguration>();
        builder.Services.AddScoped<WizardEngine>();

        // CharacterTokenServices
        builder.Services.AddScoped<ITokenCreationService, TokenCreationService>();

        // Decorators
        builder.Services.AddScoped<IButtonDecorator, ButtonDecorator>();
        builder.Services.AddScoped<IQuestionDecorator, QuestionDecorator>();

        // PortfolioServices
        builder.Services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();
        builder.Services.AddScoped<IPortfolioUpdatingService, PortfolioUpdatingService>();

        // SuggestionServices
        builder.Services.AddScoped<IPriceSuggestionService, PriceSuggestionService>();
        builder.Services.AddScoped<IQuantitySuggestionService, QuantitySuggestionService>();

        // TradeOrderServices
        builder.Services.AddScoped<IOrderCancelService, OrderCancelService>();
        builder.Services.AddScoped<IOrderCreationService, OrderCreationService>();
        builder.Services.AddScoped<IOrderValidationService, OrderValidationService>();

        // TraderServices
        builder.Services.AddScoped<ITraderBalanceUpdatingService, TraderBalanceUpdatingService>();
        builder.Services.AddScoped<ITraderRegistrationService, TraderRegistrationService>();

        var serviceProvider = builder.Services.BuildServiceProvider();

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