using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Infrastructure.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, координирует обновление PowerDeviationCoeff в бесконечном цикле")]
internal class PowerDeviationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PowerDeviationWorker> _logger;

    public PowerDeviationWorker(IServiceProvider serviceProvider, ILogger<PowerDeviationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PowerDeviationWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ArkWallet.Infrastructure.Data.ArkWalletDbContext>();
                var calculator = scope.ServiceProvider.GetRequiredService<ArkWallet.Core.TradingContext.Application.Services.MarketMaker.PowerDeviationCalculator>();

                var now = DateTime.UtcNow;
                var (state, nextExecution) = await LoadNextExecutionInfoAsync(dbContext, now, stoppingToken);
                if (nextExecution is null)
                    continue;

                var bots = await GetActiveBotsAsync(dbContext, stoppingToken);

                await RecalculateCoefficientsAsync(calculator, bots, now);

                if (bots.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                await SaveStateAsync(dbContext, state, nextExecution.Value, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PowerDeviationWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        _logger.LogInformation("PowerDeviationWorker stopped");
    }

    private static async Task<(ArkWallet.Infrastructure.Data.AppState? State, DateTime? NextExecution)> LoadNextExecutionInfoAsync(
        ArkWallet.Infrastructure.Data.ArkWalletDbContext dbContext,
        DateTime now,
        CancellationToken stoppingToken)
    {
        var state = await dbContext.AppStates.FindAsync(new object[] { "PowerDeviationNextExecution" }, stoppingToken);
        if (state is null || state.Value is null)
        {
            var nextExecution = now.AddMinutes(Random.Shared.Next(10, 361));
            return (state, nextExecution);
        }

        var dt = ParseNextExecution(state.Value);
        if (dt is not null && dt > now)
        {
            await Task.Delay(dt.Value - now, stoppingToken);
            return (null, null);
        }

        var nextExec = now.AddMinutes(Random.Shared.Next(10, 361));
        return (state, nextExec);
    }

    private static DateTime? ParseNextExecution(string value)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<DateTime?>(value);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static async Task<List<ArkWallet.Infrastructure.Data.MarketMakerBot>> GetActiveBotsAsync(
        ArkWallet.Infrastructure.Data.ArkWalletDbContext dbContext,
        CancellationToken stoppingToken)
    {
        return await dbContext.MarketMakerBots.Where(b => b.IsActive).ToListAsync(stoppingToken);
    }

    private static async Task RecalculateCoefficientsAsync(
        ArkWallet.Core.TradingContext.Application.Services.MarketMaker.PowerDeviationCalculator calculator,
        IReadOnlyList<ArkWallet.Infrastructure.Data.MarketMakerBot> bots,
        DateTime now)
    {
        foreach (var bot in bots)
        {
            var coeff = await calculator.CalculateAsync(bot.Symbol, MapRole(bot.Role), now);
            bot.PowerDeviationCoeff = coeff;
        }
    }

    private static async Task SaveStateAsync(
        ArkWallet.Infrastructure.Data.ArkWalletDbContext dbContext,
        ArkWallet.Infrastructure.Data.AppState? state,
        DateTime nextExecution,
        CancellationToken stoppingToken)
    {
        if (state is null)
        {
            dbContext.AppStates.Add(ArkWallet.Infrastructure.Data.AppState.Create("PowerDeviationNextExecution", nextExecution));
        }
        else
        {
            state.Update(nextExecution);
        }
        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private static ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole MapRole(Infrastructure.Data.BotRole role)
        => role switch
        {
            Infrastructure.Data.BotRole.Buyer => ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Buyer,
            Infrastructure.Data.BotRole.Seller => ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Seller,
            _ => throw new InvalidOperationException($"Unknown BotRole: {role}")
        };
}
