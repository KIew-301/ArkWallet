using ArkWallet.Application.Contracts.GlobalGoalServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Application.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, проверяет глобальные цели раз в два часа. Логика сервиса покрыта тестами.")]
internal class GlobalGoalUpdateWorker(
    IServiceProvider serviceProvider,
    ILogger<GlobalGoalUpdateWorker> logger) : BackgroundService
{
    private static readonly TimeSpan UpdatePeriod = TimeSpan.FromHours(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private const string LastRunKey = "GlobalGoalLastRun";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("GlobalGoalUpdateWorker started");

        var lastRun = await RestoreLastRunAsync(serviceProvider, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var checkingService = scope.ServiceProvider.GetRequiredService<IGlobalGoalCheckingService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();

                var now = DateTime.UtcNow;

                if (now - lastRun >= UpdatePeriod)
                {
                    var checkResult = await checkingService.CheckGoalsAsync();
                    if (!checkResult.IsSuccess)
                    {
                        logger.LogError("Ошибка при проверке глобальных целей: {Error}", checkResult.Message);
                        await Task.Delay(RetryDelay, stoppingToken);
                        continue;
                    }

                    lastRun = now;
                    await SaveLastRunAsync(dbContext, lastRun, stoppingToken);
                }

                await Task.Delay(UpdatePeriod, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в цикле GlobalGoalUpdateWorker");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }

        logger.LogInformation("GlobalGoalUpdateWorker stopped");
    }

    private static async Task<DateTime> RestoreLastRunAsync(IServiceProvider sp, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
        var state = await dbContext.AppStates.FindAsync([LastRunKey], ct);
        return state?.GetValue<DateTime>() ?? DateTime.MinValue;
    }

    private static async Task SaveLastRunAsync(ArkWalletDbContext dbContext, DateTime value, CancellationToken ct)
    {
        var state = await dbContext.AppStates.FindAsync([LastRunKey], ct);
        if (state is null)
            dbContext.AppStates.Add(AppState.Create(LastRunKey, value));
        else
            state.UpdateValue(value);

        await dbContext.SaveChangesAsync(ct);
    }
}
