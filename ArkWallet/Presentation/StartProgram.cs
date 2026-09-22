using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Application.Contracts.Other;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.CharacterTokenServices;
using MailQueryContract = ArkWallet.Core.MailContext.Application.Contracts.MailServices.IQueryService;
using MailQueryImpl = ArkWallet.Core.MailContext.Application.Services.MailServices.QueryService;
using ArkWallet.Core.General.Application.Contracts.Decorators;
using ArkWallet.Core.General.Application.Contracts.Leaders;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.MiningContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Core.MiningContext.Application.Contracts.Orchestrators;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.General.Application.Contracts.Other;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.GlobalGoalContext.Application.Contracts.GlobalGoalServices;
using ArkWallet.Core.MailContext.Application.Contracts.MailServices;
using ArkWallet.Core.GiftContext.Application.Contracts.GiftServices;
using ArkWallet.Core.TradingContext.Application.Contracts.SuggestionServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TraderServices;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeServices;
using ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using ArkWallet.Core.General.Application.Services.Leaders;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.MiningContext.Application.Services.MiningMachineServices;
using ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices;
using ArkWallet.Core.ShoppingContext.Application.Services.Orchestrators;
using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.TradingContext.Application.Services.Other;
using ArkWallet.Core.GlobalGoalContext.Application.Services.GlobalGoalServices;
using ArkWallet.Core.MailContext.Application.Services.MailServices;
using ArkWallet.Core.GiftContext.Application.Services.GiftServices;
using ArkWallet.Core.PortfolioContext.Application.Services.PortfolioServices;
using ArkWallet.Core.TradingContext.Application.Services.SuggestionServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeServices;
using ArkWallet.Core.General.Application.Services.Wizard;
using ArkWallet.Infrastructure.Workers;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.API;
using ArkWallet.Presentation.Health;
using ArkWallet.Presentation.Wizard;
using ArkWallet.Infrastructure.AccessControl;
using ArkWallet.Telegram;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.RateLimiting;
using ArkWallet.Core.ShoppingContext.Application.Contracts.Orchestrators;
using ArkWallet.Core.MiningContext.Application.Services.Orchestrators;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseHistoryServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionPurchaseHistoryServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionExpiryServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;
using ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionActivationServices;
using ArkWallet.Infrastructure.Payment;

[ExcludeFromCodeCoverage(Justification = "Точка входа приложения: конфигурация DI, middleware и инфраструктуры. Не содержит бизнес-логики.")]
class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var isTesting = builder.Environment.EnvironmentName == "Testing";

        builder.Services.AddLogging(builder => builder.AddConsole());

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowAnyOrigin();
            });
        });

        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        builder.Configuration.AddConfiguration(configuration);

        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer(options =>
            {
                var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]);

                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
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

        builder.Services.AddAuthorization();
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<AccessSettingFilter>();
        });
        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        // Анти-спам API: скользящее окно, 1 запрос/с на каждый (клиент, эндпоинт).
        // Auth-эндпоинты строже: 1 запрос / 5 секунд на клиента.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? ((long)retryAfter.TotalSeconds).ToString()
                    : "1";
                await Task.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                string client = GetClientKey(context);
                bool isAuth = context.Request.Path.StartsWithSegments("/api/v1/auth");
                bool isCandles = context.Request.Path.StartsWithSegments("/api/v1/tokens/candle");
                return RateLimitPartition.GetSlidingWindowLimiter(client + "|" + context.Request.Path, _ =>
                    new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = isCandles ? 3 : 1,
                        Window = isAuth ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(1),
                        SegmentsPerWindow = isAuth ? 5 : 1,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        static string GetClientKey(HttpContext context)
        {
            string? forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter("Npgsql")
                .AddMeter(ArkWalletMetrics.Meter.Name)
                .AddPrometheusExporter());

        builder.Services.AddSingleton<IConfiguration>(configuration);

        // Main
        var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
        if (dbProvider == "PostgreSQL")
        {
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=arkwallet;Username=arkwallet;Password=arkwallet";
            builder.Services.AddDbContext<ArkWalletDbContext>(options =>
                options.UseNpgsql(connStr)
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        }
        else
        {
            builder.Services.AddDbContext<ArkWalletDbContext>(options =>
                options.UseSqlite("Data Source=arkwallet.db")
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        }

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ArkWallet API",
                Version = "v1",
                Description = "API для платформы ArkWallet",
                Contact = new OpenApiContact
                {
                    Name = "ArkWallet Team",
                    Email = "support@arkwallet.com"
                }
            });

            // Настройка JWT для Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Включить XML комментарии для документации
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        // Services
        RegisterServices(builder.Services, builder.Configuration);

        // Background Services
        if (!isTesting)
        {
            builder.Services.AddHostedService<MarketMakerWorker>();
            builder.Services.AddHostedService<MarketWallBlockerWorker>();
            builder.Services.AddHostedService<NotificationWorker>();
            builder.Services.AddHostedService<BalanceSavingSnapshotWorker>();
            builder.Services.AddHostedService<MiningMachineCalculationWorker>();
            builder.Services.AddHostedService<MiningGlobalRuleCreationWorker>();
            builder.Services.AddHostedService<MiningMachineSlotSwitchingWorker>();
            builder.Services.AddHostedService<PowerDeviationWorker>();
            builder.Services.AddHostedService<GlobalGoalUpdateWorker>();
            builder.Services.AddHostedService<SubscriptionExpiryWorker>();
            builder.Services.AddHostedService<PaymentConfirmationWorker>();
            builder.Services.AddHostedService<SubscriptionRenewalWorker>();
        }

        var app = builder.Build();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ArkWallet API v1");
            c.RoutePrefix = "swagger";
        });
        
        app.UseHttpsRedirection();
        // Глобальный кап анти-спама: не более 8 запросов/с с одного клиента на API (свечи — 3/с).
        // (на случай, когда спамят на разные эндпоинты — у каждого свой бакет основного лимитера).
        app.UseRateLimiter(new RateLimiterOptions
        {
            RejectionStatusCode = StatusCodes.Status429TooManyRequests,
            OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? ((long)retryAfter.TotalSeconds).ToString()
                    : "1";
                await Task.CompletedTask;
            },
            GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                string client = GetClientKey(context);
                bool isApi = context.Request.Path.StartsWithSegments("/api");
                bool isCandles = context.Request.Path.StartsWithSegments("/api/v1/tokens/candle");
                int permit = !isApi ? 1000 : isCandles ? 3 : 8;
                return RateLimitPartition.GetSlidingWindowLimiter(client + "|" + (isApi ? "api" : "web"),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = permit,
                        Window = TimeSpan.FromSeconds(1),
                        SegmentsPerWindow = 1,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            })
        });
        app.UseRateLimiter();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapHealthChecks("/health").DisableRateLimiting();
        app.UseMiddleware<MetricsApiKeyMiddleware>();
        app.MapPrometheusScrapingEndpoint();

        // Применение миграций
        if (!isTesting)
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
                await db.Database.MigrateAsync();
                Console.WriteLine("Миграции применены!");
            }

            // Load AccessControl into memory
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
                var accessControl = app.Services.GetRequiredService<AccessControlService>();
                var setting = await db.AccessSettings.FirstOrDefaultAsync();
                if (setting == null)
                {
                    setting = AccessSetting.Create();
                    db.AccessSettings.Add(setting);
                    await db.SaveChangesAsync();
                }
                accessControl.LoadFromDb(setting);
                Console.WriteLine("AccessSetting loaded into memory.");
            }

            // Seed basic subscription (Level 1)
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
                var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.Level == 1);
                if (sub == null)
                {
                    sub = new Subscription
                    {
                        Name = "Базовая",
                        Level = 1,
                        PriceRubles = 0,
                        MaxOrders = 5,
                        MaxMiningMachines = 5,
                        DurationMinutes = null
                    };
                    db.Subscriptions.Add(sub);
                    await db.SaveChangesAsync();
                }
                Console.WriteLine("Basic subscription loaded.");
            }

            // Telegram Bot
            var bot = app.Services.GetRequiredService<TelegramBot>();
            await bot.Start();
        }

        await app.RunAsync();
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // DbContext
        services.AddDbContext<ArkWalletDbContext>();

        // Domain Engines
        services.AddScoped<TradingEngine>();
        services.AddScoped<MarketMakerGridEngine>();
        services.AddScoped<WallBlockerEngine>();
        services.AddScoped<MiningEngine>();

        // Domain Events
        services.AddScoped<IEventPublisher, MediatREventPublisher>();

        // Telegram Bot
        services.AddSingleton<TelegramBot>();
        services.AddSingleton<IMessageSender>(sp => sp.GetRequiredService<TelegramBot>());

        // RabbitMQ
        services.AddSingleton<RabbitMQService>();
        services.AddScoped<ITaskDispatcher, RabbitMQTaskDispatcher>();

        // Wizard
        services.AddSingleton<IUserSessionStore, UserSessionStore>();
        services.AddScoped<WizardConfiguration>();
        services.AddScoped<WizardEngine>();

        // CharacterTokenServices
        services.AddScoped<ITokenCreationService, TokenCreationService>();
        services.AddScoped<ITokenDeletionService, TokenDeletionService>();
        services.AddScoped<ITokenPriceCandleUpdateService, TokenPriceCandleUpdateService>();
        services.AddScoped<ICandleAggregatorService, CandleAggregatorService>();
        services.AddScoped<ITokenQueryService, TokenQueryService>();
        services.AddScoped<ITokenMediaUpdateService, TokenMediaUpdateService>();
        services.AddScoped<ITokenPriceChangesCalculationService, TokenPriceChangeCalculationService>();
        services.AddScoped<ITokenPriceCandleQueryService, TokenPriceCandleQueryService>();
        services.AddScoped<ICandleOrchestrator, CandleOrchestrator>();

        // Decorators
        services.AddScoped<IButtonDecorator, ButtonDecorator>();
        services.AddScoped<IQuestionDecorator, QuestionDecorator>();

        // PortfolioServices
        services.AddScoped<ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices.IQueryService, ArkWallet.Core.PortfolioContext.Application.Services.PortfolioServices.QueryService>();
        services.AddScoped<ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices.IUpdatingService, ArkWallet.Core.PortfolioContext.Application.Services.PortfolioServices.UpdatingService>();

        // GlobalGoalServices
        services.AddScoped<IGlobalGoalCheckingService, GlobalGoalCheckingService>();
        services.AddScoped<IGlobalGoalQueryService, GlobalGoalQueryService>();
        services.AddScoped<IGlobalGoalCreationService, GlobalGoalCreationService>();
        services.AddScoped<IDomainGlobalGoalCalculation, TotalBalanceGlobalGoalCalculation>();

        // MailServices
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<MailQueryContract, MailQueryImpl>();
        services.AddScoped<IStatusUpdatingService, StatusUpdatingService>();
        // GiftServices
        services.AddScoped<ISendingService, SendingService>();

        // SuggestionServices
        services.AddScoped<IPriceSuggestionService, PriceSuggestionService>();
        services.AddScoped<IQuantitySuggestionService, QuantitySuggestionService>();

        // TradeOrderServices
        services.AddScoped<IOrderCancellationService, OrderCancellationService>();
        services.AddScoped<IOrderCreationService, OrderCreationService>();
        services.AddScoped<IOrderValidationService, OrderValidationService>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        services.AddScoped<IOrderBookService, OrderBookService>();

        // TraderServices
        services.AddScoped<ITraderBalanceUpdatingService, TraderBalanceUpdatingService>();
        services.AddScoped<ITraderRegistrationService, TraderRegistrationService>();
        services.AddScoped<ITraderQueryService, TraderQueryService>();
        services.AddScoped<IBalanceSnapshotService, BalanceSnapshotService>();
        services.AddScoped<IBalanceChangesCalculationService, BalanceChangesCalculationService>();
        services.AddScoped<IBalanceSavingService, BalanceSavingService>();
        services.AddScoped<IBalanceSnapshotOrchestrator, BalanceSnapshotOrchestrator>();

        // MarketMaker
        services.AddScoped<IMarketMakerBotRegistrationService, MarketMakerBotRegistrationService>();
        services.AddScoped<PowerDeviationCalculator>();
        services.AddScoped<IMarketMakerBotQueryService, MarketMakerBotQueryService>();
        services.AddScoped<IMarketMakerOrchestrator, MarketMakerOrchestrator>();
        services.AddScoped<IMarketMakerOrderService, MarketMakerOrderService>();

        // MarketWallBlocker
        services.AddScoped<IMarketWallBlockerOrchestrator, MarketWallBlockerOrchestrator>();

        // MiningMachineServices
        services.AddScoped<ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices.IMachineCreationService, ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices.MachineCreationService>();
        services.AddScoped<ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices.IMachineUpdateService, ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices.MachineUpdateService>();
        services.AddScoped<IMiningMachineRuleCreationService, MiningMachineRuleCreationService>();
        services.AddScoped<IMiningMachineRuleUpdateService, MiningMachineRuleUpdateService>();
        services.AddScoped<ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices.IMachineSlotBuyingService, ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices.MachineSlotBuyingService>();
        services.AddScoped<IMiningMachineSlotSwitchingService, MiningMachineSlotSwitchingService>();
        services.AddScoped<IMiningMachineSlotCalculationService, MiningMachineSlotCalculationService>();
        services.AddScoped<IMiningMachineSlotSellingService, MiningMachineSlotSellingService>();
        services.AddScoped<ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices.IMachineQueryService, ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices.MachineQueryService>();
        services.AddScoped<IMiningMachineSlotQueryService, MiningMachineSlotQueryService>();
        services.AddScoped<IMiningMachineSlotTakingTokenService, MiningMachineSlotTakingTokenService>();
        services.AddScoped<IMiningGlobalRuleQueryService, MiningGlobalRuleQueryService>();
        services.AddScoped<IMiningGlobalRuleCreationService, MiningGlobalRuleCreationService>();
        services.AddScoped<ArkWallet.Core.ShoppingContext.Application.Contracts.MiningMachineServices.IMachineDeletionService, ArkWallet.Core.ShoppingContext.Application.Services.MiningMachineServices.MachineDeletionService>();
        services.AddScoped<IMiningMachineRuleDeletionService, MiningMachineRuleDeletionService>();
        services.AddScoped<IMiningGlobalRuleUpdateService, MiningGlobalRuleUpdateService>();
        services.AddScoped<IAppStateQueryService, AppStateQueryService>();

        // MiningMachineOrchestrators
        services.AddScoped<ArkWallet.Core.ShoppingContext.Application.Contracts.Orchestrators.IMachineCreationOrchestrator, ArkWallet.Core.ShoppingContext.Application.Services.Orchestrators.MachineCreationOrchestrator>();
        services.AddScoped<IMiningMachineSlotTakingTokenOrchestrator, MiningMachineSlotTakingTokenOrchestrator>();
        services.AddScoped<IMiningMachineSlotSwitchingOrchestrator, MiningMachineSlotSwitchingOrchestrator>();
        services.AddScoped<IMiningMachineSlotSellingOrchestrator, MiningMachineSlotSellingOrchestrator>();

        // Trade Services
        services.AddScoped<ITradeQueryService, TradeQueryService>();

        // Leaders
        services.AddScoped<ILeadersTopByBalanceQueryService, LeadersTopByBalanceQueryService>();

        // Other
        services.AddScoped<ITraderAuthService, TraderAuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITradingVolumeService, TradingVolumeService>();

        // Observability
        services.AddSingleton<IMetricsSnapshotService, MetricsSnapshotService>();

        // Subscriptions
        services.AddScoped<ISubscriptionQueryService, SubscriptionQueryService>();
        services.AddScoped<IPurchaseService, SubscriptionPurchaseService>();
        services.AddScoped<IPurchaseHistoryQueryService, SubscriptionPurchaseHistoryQueryService>();
        services.AddScoped<ISubscriptionExpiryService, SubscriptionExpiryService>();
        services.AddScoped<ISubscriptionActivationService, SubscriptionActivationService>();

        var paymentProvider = configuration["Payment:Provider"] ?? "Instant";
        if (string.Equals(paymentProvider, "YooKassa", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<YooKassaOptions>(configuration.GetSection("YooKassa"));
            services.AddHttpClient<IPaymentIntegrationService, YooKassaPaymentIntegrationService>(client =>
            {
                client.BaseAddress = new Uri("https://api.yookassa.ru/v3");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            services.AddScoped<IPaymentIntegrationService, InstantSuccessPaymentService>();
        }
        services.AddSingleton(TimeProvider.System);

        // Access Control
        services.AddSingleton<AccessControlService>();
    }
}