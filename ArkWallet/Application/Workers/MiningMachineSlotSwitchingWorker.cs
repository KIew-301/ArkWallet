using ArkWallet.Application.Contracts.MiningMachineServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Application.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, координирует вызовы сервиса в бесконечном цикле. Тестируется через сервис.")]
internal class MiningMachineSlotSwitchingWorker : BackgroundService
{
    private const int IntervalMinutes = 2;
    private const int FailureCooldownSeconds = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MiningMachineSlotSwitchingWorker> _logger;

    public MiningMachineSlotSwitchingWorker(IServiceProvider serviceProvider, ILogger<MiningMachineSlotSwitchingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MiningMachineSlotSwitchingWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var switchingService = scope.ServiceProvider.GetRequiredService<IMiningMachineSlotSwitchingService>();

                var result = await switchingService.CheckSwitchingAsync();
                if (!result.IsSuccess)
                {
                    _logger.LogError("Failed to check machine switching: {Error}", result.Message);
                    await Task.Delay(TimeSpan.FromSeconds(FailureCooldownSeconds), stoppingToken);
                    continue;
                }

                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MiningMachineSlotSwitchingWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(FailureCooldownSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("MiningMachineSlotSwitchingWorker stopped");
    }
}
