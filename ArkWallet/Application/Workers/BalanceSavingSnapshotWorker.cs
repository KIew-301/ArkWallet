using ArkWallet.Application.Contracts.TraderServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Application.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, делегирует работу IBalanceSnapshotOrchestrator. Логика оркестратора уже покрыта тестами.")]
internal class BalanceSavingSnapshotWorker(
    IServiceProvider serviceProvider,
    ILogger<BalanceSavingSnapshotWorker> logger) : BackgroundService
{
    private const int UpdatePeriodInSeconds = 60 * 60 * 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IBalanceSnapshotOrchestrator>();

                var result = await orchestrator.CreateSnapshotsForAllTradersAsync();

                if (!result.IsSuccess)
                {
                    logger.LogError("Ошибка при создании снимков баланса: {Error}", result.Message);
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                logger.LogInformation("Снимки баланса успешно созданы");
                await Task.Delay(TimeSpan.FromSeconds(UpdatePeriodInSeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в цикле BalanceSavingSnapshotWorker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
