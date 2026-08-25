using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Application.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, координирует вызовы сервиса в бесконечном цикле. Тестируется через сервис.")]
internal class MiningGlobalRuleCreationWorker : BackgroundService
{
    private const string LastUpdateKey = "MiningGlobalRuleLastUpdate";
    private const int TickMinutes = 1;
    private const int FailureCooldownSeconds = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MiningGlobalRuleCreationWorker> _logger;

    public MiningGlobalRuleCreationWorker(IServiceProvider serviceProvider, ILogger<MiningGlobalRuleCreationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MiningGlobalRuleCreationWorker started");
        var lastUpdate = await RestoreLastUpdateAsync(_serviceProvider, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var creationService = scope.ServiceProvider.GetRequiredService<IMiningGlobalRuleCreationService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();

                var now = DateTime.UtcNow;
                if (now.Date <= lastUpdate.Date)
                {
                    await Task.Delay(TimeSpan.FromMinutes(TickMinutes), stoppingToken);
                    continue;
                }

                var result = await creationService.CreateRulesAsync();
                if (!result.IsSuccess)
                {
                    _logger.LogError("Failed to create global mining rules: {Error}", result.Message);
                    await Task.Delay(TimeSpan.FromSeconds(FailureCooldownSeconds), stoppingToken);
                    continue;
                }

                lastUpdate = now;
                await SaveLastUpdateAsync(dbContext, now, stoppingToken);

                await Task.Delay(TimeSpan.FromMinutes(TickMinutes), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MiningGlobalRuleCreationWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(FailureCooldownSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("MiningGlobalRuleCreationWorker stopped");
    }

    private static async Task<DateTime> RestoreLastUpdateAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
        var state = await dbContext.AppStates.FindAsync([LastUpdateKey], stoppingToken);
        return state?.GetValue<DateTime>() ?? DateTime.MinValue;
    }

    private static async Task SaveLastUpdateAsync(ArkWalletDbContext dbContext, DateTime now, CancellationToken stoppingToken)
    {
        var state = await dbContext.AppStates.FindAsync([LastUpdateKey], stoppingToken);
        if (state == null)
            dbContext.AppStates.Add(AppState.Create(LastUpdateKey, now));
        else
            state.UpdateValue(now);

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
