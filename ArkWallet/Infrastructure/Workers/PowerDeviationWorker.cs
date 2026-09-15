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

                // 1. Чтение AppState
                var state = await dbContext.AppStates.FindAsync("PowerDeviationNextExecution", stoppingToken);
                var now = DateTime.UtcNow;
                var nextExecution = now.AddMinutes(Random.Shared.Next(10, 361));

                if (state is { } s && s.Value is not null)
                {
                    try
                    {
                        var dt = System.Text.Json.JsonSerializer.Deserialize<DateTime?>(s.Value);
                        if (dt is not null && dt > now)
                        {
                            await Task.Delay(dt.Value - now, stoppingToken);
                            continue;
                        }
                    }
                    catch
                    {
                        // десериализация не удалась — продолжаем по логике создания новой записи
                    }
                }

                // 2. Получить активных ботов
                var bots = await dbContext.MarketMakerBots.Where(b => b.IsActive).ToListAsync(stoppingToken);

                // 3. Пересчёт коэффициентов
                foreach (var bot in bots)
                {
                    var coeff = await calculator.CalculateAsync(bot.Symbol, MapRole(bot.Role), now, stoppingToken);
                    bot.PowerDeviationCoeff = coeff;
                }

                // 4. Сохранение изменений ботов
                if (bots.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                // 5. Запись нового времени выполнения
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PowerDeviationWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        _logger.LogInformation("PowerDeviationWorker stopped");
    }

    private static ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole MapRole(Infrastructure.Data.BotRole role)
        => role switch
        {
            Infrastructure.Data.BotRole.Buyer => ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Buyer,
            Infrastructure.Data.BotRole.Seller => ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate.MarketMakerRole.Seller,
            _ => throw new InvalidOperationException($"Unknown BotRole: {role}")
        };
}
