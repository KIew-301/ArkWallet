using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Application.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, координирует вызовы сервиса в бесконечном цикле. Тестируется через сервис.")]
internal class MiningMachineCalculationWorker : BackgroundService
{
    private const string LastCalculationKey = "MiningMachineLastCalculation";
    private const int IntervalMinutes = 10;
    private const int FailureCooldownSeconds = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MiningMachineCalculationWorker> _logger;
    private DateTime _lastCalculation;

    public MiningMachineCalculationWorker(IServiceProvider serviceProvider, ILogger<MiningMachineCalculationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MiningMachineCalculationWorker started");
        _lastCalculation = await RestoreLastCalculationAsync(_serviceProvider, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var calculationService = scope.ServiceProvider.GetRequiredService<IMiningMachineSlotCalculationService>();
                var miningEngine = scope.ServiceProvider.GetRequiredService<MiningEngine>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();

                var now = DateTime.UtcNow;
                var timingCoeff = miningEngine.CalculateTimingCoeff(now, _lastCalculation);

                var result = await calculationService.TakeTokensOnMachinesAsync(timingCoeff);
                if (!result.IsSuccess)
                {
                    _logger.LogError("Failed to calculate mining tokens: {Error}", result.Message);
                    await Task.Delay(TimeSpan.FromSeconds(FailureCooldownSeconds), stoppingToken);
                    continue;
                }

                _lastCalculation = now;
                await SaveLastCalculationAsync(dbContext, now, stoppingToken);

                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MiningMachineCalculationWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(FailureCooldownSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("MiningMachineCalculationWorker stopped");
    }

    private static async Task<DateTime> RestoreLastCalculationAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
        var state = await dbContext.AppStates.FindAsync([LastCalculationKey], stoppingToken);
        return state?.GetValue<DateTime>() ?? DateTime.UtcNow;
    }

    private static async Task SaveLastCalculationAsync(ArkWalletDbContext dbContext, DateTime now, CancellationToken stoppingToken)
    {
        var state = await dbContext.AppStates.FindAsync([LastCalculationKey], stoppingToken);
        if (state == null)
            dbContext.AppStates.Add(AppState.Create(LastCalculationKey, now));
        else
            state.UpdateValue(now);

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
