using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Decorators;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.SuggestionServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.Leaders;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Application.Services.MiningMachineServices;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Application.Services.Other;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.SuggestionServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Application.Services.TradeServices;
using ArkWallet.Application.Services.Wizard;
using ArkWallet.Application.Workers;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Entities.Configurations;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Infrastructure.Wizard;
using ArkWallet.Presentation.Health;
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
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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
        builder.Services.AddControllers();
        builder.Services.AddRouting(options => options.LowercaseUrls = true);

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
                options.UseNpgsql(connStr));
        }
        else
        {
            builder.Services.AddDbContext<ArkWalletDbContext>(options =>
                options.UseSqlite("Data Source=arkwallet.db"));
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
        RegisterServices(builder.Services);

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
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.UseMiddleware<MetricsApiKeyMiddleware>();
        app.MapPrometheusScrapingEndpoint();

        // Применение миграций
        if (!isTesting)
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
                var provider = app.Services.GetRequiredService<IConfiguration>()["Database:Provider"] ?? "SQLite";
                if (provider == "PostgreSQL")
                {
                    await db.Database.EnsureCreatedAsync();
                    await EnsureMiningTablesAsync(db);
                    Console.WriteLine("PostgreSQL schema ensured!");
                }
                else
                {
                    await db.Database.MigrateAsync();
                    Console.WriteLine("Миграции применены!");
                }
            }

            // Telegram BotS
            using (var scope = app.Services.CreateScope())
            {
                var bot = scope.ServiceProvider.GetRequiredService<TelegramBot>();
                await bot.Start();
            }
        }

        await app.RunAsync();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // DbContext
        services.AddDbContext<ArkWalletDbContext>();

        // Domain Engines
        services.AddScoped<TradingEngine>();
        services.AddScoped<FixedGridEngine>();
        services.AddScoped<MarketMakerGridEngine>();
        services.AddScoped<WallBlockerEngine>();
        services.AddScoped<MiningEngine>();

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
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();
        services.AddScoped<IPortfolioUpdatingService, PortfolioUpdatingService>();

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
        services.AddScoped<IMarketMakerBotQueryService, MarketMakerBotQueryService>();
        services.AddScoped<IMarketMakerOrchestrator, MarketMakerOrchestrator>();
        services.AddScoped<IMarketMakerOrderService, MarketMakerOrderService>();

        // MarketWallBlocker
        services.AddScoped<IMarketWallBlockerOrchestrator, MarketWallBlockerOrchestrator>();

        // MiningMachineServices
        services.AddScoped<IMiningMachineCreationService, MiningMachineCreationService>();
        services.AddScoped<IMiningMachineUpdateService, MiningMachineUpdateService>();
        services.AddScoped<IMiningMachineRuleCreationService, MiningMachineRuleCreationService>();
        services.AddScoped<IMiningMachineRuleUpdateService, MiningMachineRuleUpdateService>();
        services.AddScoped<IMiningMachineSlotBuyingService, MiningMachineSlotBuyingService>();
        services.AddScoped<IMiningMachineSlotSwitchingService, MiningMachineSlotSwitchingService>();
        services.AddScoped<IMiningMachineSlotCalculationService, MiningMachineSlotCalculationService>();
        services.AddScoped<IMiningMachineSlotSellingService, MiningMachineSlotSellingService>();
        services.AddScoped<IMiningMachineQueryService, MiningMachineQueryService>();
        services.AddScoped<IMiningMachineSlotQueryService, MiningMachineSlotQueryService>();
        services.AddScoped<IMiningMachineSlotTakingTokenService, MiningMachineSlotTakingTokenService>();
        services.AddScoped<IMiningGlobalRuleQueryService, MiningGlobalRuleQueryService>();
        services.AddScoped<IMiningGlobalRuleCreationService, MiningGlobalRuleCreationService>();
        services.AddScoped<IMiningMachineDeletionService, MiningMachineDeletionService>();
        services.AddScoped<IMiningMachineRuleDeletionService, MiningMachineRuleDeletionService>();
        services.AddScoped<IMiningGlobalRuleUpdateService, MiningGlobalRuleUpdateService>();
        services.AddScoped<IAppStateQueryService, AppStateQueryService>();

        // MiningMachineOrchestrators
        services.AddScoped<IMiningMachineCreationOrchestrator, MiningMachineCreationOrchestrator>();
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
    }

    /// <summary>
    /// Идемпотентно создаёт таблицы майнинга для PostgreSQL,
    /// если они ещё не были созданы (EnsureCreated не добавляет новые таблицы в существующую БД).
    /// </summary>
    private static async Task EnsureMiningTablesAsync(ArkWalletDbContext db)
    {
        const string miningMachines = """
            CREATE TABLE IF NOT EXISTS "MiningMachines" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "Name" text NOT NULL,
                "Type" text NOT NULL,
                "SwitchingTime" integer NOT NULL,
                "Reusability" numeric NOT NULL,
                "IsActiveForSale" boolean NOT NULL,
                "Cost" numeric NOT NULL,
                "Efficiency" numeric NOT NULL DEFAULT 1,
                "Image" text NOT NULL,
                CONSTRAINT "PK_MiningMachines" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MiningMachines_Name" ON "MiningMachines" ("Name");
            """;

        const string miningMachineRules = """
            CREATE TABLE IF NOT EXISTS "MiningMachineRules" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "MiningMachineId" bigint NOT NULL,
                "CharacterTokenId" text NOT NULL,
                "MiningCoefficient" numeric NOT NULL,
                CONSTRAINT "PK_MiningMachineRules" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_MiningMachineRules_MiningMachines_MiningMachineId"
                    FOREIGN KEY ("MiningMachineId") REFERENCES "MiningMachines" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_MiningMachineRules_CharacterTokens_CharacterTokenId"
                    FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MiningMachineRules_MiningMachineId_CharacterTokenId"
                ON "MiningMachineRules" ("MiningMachineId", "CharacterTokenId");
            """;

        const string miningGlobalRules = """
            CREATE TABLE IF NOT EXISTS "MiningGlobalRules" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "TokenId" text NOT NULL,
                "CurrentCoefficient" numeric NOT NULL,
                "FutureCoefficient" numeric NOT NULL,
                "BaseTokenMiningSpeed" numeric NOT NULL,
                CONSTRAINT "PK_MiningGlobalRules" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_MiningGlobalRules_CharacterTokens_TokenId"
                    FOREIGN KEY ("TokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MiningGlobalRules_TokenId" ON "MiningGlobalRules" ("TokenId");
            """;

        const string miningMachineSlots = """
            CREATE TABLE IF NOT EXISTS "MiningMachineSlots" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "TraderId" bigint NOT NULL,
                "TokenId" text NULL,
                "MiningGlobalRuleId" bigint NULL,
                "Status" text NOT NULL,
                "StartSwitchingDateTime" timestamptz NULL,
                "EndSwitchingDateTime" timestamptz NULL,
                "TokensAmountCollected" numeric NOT NULL,
                "Cost" numeric NOT NULL,
                "CreatedAt" timestamptz NOT NULL,
                "SoldAt" timestamptz NULL,
                "Name" text NOT NULL DEFAULT '',
                "Type" text NOT NULL DEFAULT '',
                "SwitchingTime" integer NOT NULL DEFAULT 0,
                "Efficiency" numeric NOT NULL DEFAULT 0,
                "Image" text NOT NULL DEFAULT '',
                CONSTRAINT "PK_MiningMachineSlots" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_MiningMachineSlots_Traders_TraderId"
                    FOREIGN KEY ("TraderId") REFERENCES "Traders" ("TelegramId") ON DELETE RESTRICT,
                CONSTRAINT "FK_MiningMachineSlots_CharacterTokens_TokenId"
                    FOREIGN KEY ("TokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT,
                CONSTRAINT "FK_MiningMachineSlots_MiningGlobalRules_MiningGlobalRuleId"
                    FOREIGN KEY ("MiningGlobalRuleId") REFERENCES "MiningGlobalRules" ("Id") ON DELETE RESTRICT
            );
            """;

        const string miningMachineSlotRules = """
            CREATE TABLE IF NOT EXISTS "MiningMachineSlotRules" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "MiningMachineSlotId" bigint NOT NULL,
                "CharacterTokenId" text NOT NULL,
                "MiningCoefficient" numeric NOT NULL,
                CONSTRAINT "PK_MiningMachineSlotRules" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_MiningMachineSlotRules_MiningMachineSlots_MiningMachineSlotId"
                    FOREIGN KEY ("MiningMachineSlotId") REFERENCES "MiningMachineSlots" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_MiningMachineSlotRules_CharacterTokens_CharacterTokenId"
                    FOREIGN KEY ("CharacterTokenId") REFERENCES "CharacterTokens" ("Symbol") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MiningMachineSlotRules_MiningMachineSlotId_CharacterTokenId"
                ON "MiningMachineSlotRules" ("MiningMachineSlotId", "CharacterTokenId");
            CREATE INDEX IF NOT EXISTS "IX_MiningMachineSlotRules_CharacterTokenId"
                ON "MiningMachineSlotRules" ("CharacterTokenId");
            """;

        // ╨Ф╨╛╨▓╨╡╨┤╨╡╨╜╨╕╨╡ ╤Б╤Г╤Й╨╡╤Б╤В╨▓╤Г╤О╤Й╨╡╨╣ ╨С╨Ф ╨┤╨╛ ╨╜╨╛╨▓╨╛╨╣ ╤Б╤Е╨╡╨╝╤Л: ╨┤╨╛╨▒╨░╨▓╨╗╤П╨╡╨╝ ╨║╨╛╨┐╨╕╤А╤Г╨╡╨╝╤Л╨╡ ╤Е╨░╤А╨░╨║╤В╨╡╤А╨╕╤Б╤В╨╕╨║╨╕ ╤Б╨╗╨╛╤В╨░,
        // ╨┐╨╡╤А╨╡╨╜╨╛╤Б╨╕╨╝ ╨┤╨░╨╜╨╜╤Л╨╡ ╨╕╨╖ ╨║╨░╤В╨░╨╗╨╛╨│╨░ ╨╝╨░╤И╨╕╨╜ ╨╕ ╨┐╨╡╤А╨╡╨╜╨╛╤Б╨╕╨╝ ╨┐╤А╨░╨▓╨╕╨╗╨░, ╨╖╨░╤В╨╡╨╝ ╤Г╨┤╨░╨╗╤П╨╡╨╝ ╤Б╤В╨░╤А╤Л╨╡ ╤Б╤Б╤Л╨╗╨║╨╕.
        const string miningMachineSlotsMigration = """
            ALTER TABLE "MiningMachineSlots" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "MiningMachineSlots" ADD COLUMN IF NOT EXISTS "Type" text NOT NULL DEFAULT '';
            ALTER TABLE "MiningMachineSlots" ADD COLUMN IF NOT EXISTS "SwitchingTime" integer NOT NULL DEFAULT 0;
            ALTER TABLE "MiningMachineSlots" ADD COLUMN IF NOT EXISTS "Efficiency" numeric NOT NULL DEFAULT 0;
            ALTER TABLE "MiningMachineSlots" ADD COLUMN IF NOT EXISTS "Image" text NOT NULL DEFAULT '';
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_name = 'MiningMachineSlots' AND column_name = 'MiningMachineId') THEN
                    UPDATE "MiningMachineSlots" s
                    SET "Name" = m."Name",
                        "Type" = m."Type",
                        "SwitchingTime" = m."SwitchingTime",
                        "Efficiency" = m."Efficiency",
                        "Image" = m."Image"
                    FROM "MiningMachines" m
                    WHERE m."Id" = s."MiningMachineId";

                    INSERT INTO "MiningMachineSlotRules" ("MiningMachineSlotId", "CharacterTokenId", "MiningCoefficient")
                    SELECT s."Id", r."CharacterTokenId", r."MiningCoefficient"
                    FROM "MiningMachineSlots" s
                    INNER JOIN "MiningMachineRules" r ON r."MiningMachineId" = s."MiningMachineId"
                    ON CONFLICT ("MiningMachineSlotId", "CharacterTokenId") DO NOTHING;

                    DROP INDEX IF EXISTS "IX_MiningMachineSlots_MiningMachineId";
                    DROP INDEX IF EXISTS "IX_MiningMachineSlots_MachineRuleId";
                    ALTER TABLE "MiningMachineSlots" DROP CONSTRAINT IF EXISTS "FK_MiningMachineSlots_MiningMachines_MiningMachineId";
                    ALTER TABLE "MiningMachineSlots" DROP CONSTRAINT IF EXISTS "FK_MiningMachineSlots_MiningMachineRules_MachineRuleId";
                    ALTER TABLE "MiningMachineSlots" DROP COLUMN IF EXISTS "MiningMachineId";
                    ALTER TABLE "MiningMachineSlots" DROP COLUMN IF EXISTS "MachineRuleId";
                END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS "IX_MiningMachineSlots_TraderId" ON "MiningMachineSlots" ("TraderId");
            CREATE INDEX IF NOT EXISTS "IX_MiningMachineSlots_Status" ON "MiningMachineSlots" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_MiningMachineSlots_TokenId" ON "MiningMachineSlots" ("TokenId");
            CREATE INDEX IF NOT EXISTS "IX_MiningMachineSlots_MiningGlobalRuleId" ON "MiningMachineSlots" ("MiningGlobalRuleId");
            """;

        await db.Database.ExecuteSqlRawAsync(miningMachines);
        await db.Database.ExecuteSqlRawAsync(miningMachineRules);
        await db.Database.ExecuteSqlRawAsync(miningGlobalRules);
        await db.Database.ExecuteSqlRawAsync(miningMachineSlots);
        await db.Database.ExecuteSqlRawAsync(miningMachineSlotRules);
        await db.Database.ExecuteSqlRawAsync(miningMachineSlotsMigration);
    }
}