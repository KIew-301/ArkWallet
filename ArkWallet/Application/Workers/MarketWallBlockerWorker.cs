using ArkWallet.Application.Contracts.Orchestrators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace ArkWallet.Application.Workers;

[ExcludeFromCodeCoverage(Justification = "Фоновый воркер, координирует вызовы оркестратора в бесконечном цикле. Тестируется через оркестратор и отдельные сервисы.")]
internal class MarketWallBlockerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketWallBlockerWorker> _logger;

    public MarketWallBlockerWorker(IServiceProvider serviceProvider, ILogger<MarketWallBlockerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketWallBlockerWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IMarketWallBlockerOrchestrator>();

                var registerResult = await orchestrator.EnsureRegisteredAsync();
                if (!registerResult.IsSuccess)
                {
                    _logger.LogError("Failed to ensure WallBlocker trader registered: {Error}", registerResult.Message);
                }

                var balanceResult = await orchestrator.EnsureBalancesAsync();
                if (!balanceResult.IsSuccess)
                {
                    _logger.LogError("Failed to ensure WallBlocker balances: {Error}", balanceResult.Message);
                }

                var iterationResult = await orchestrator.ExecuteIterationAsync();
                if (!iterationResult.IsSuccess)
                {
                    _logger.LogError("Failed to execute WallBlocker iteration: {Error}", iterationResult.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarketWallBlockerWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("MarketWallBlockerWorker stopped");
    }
}
