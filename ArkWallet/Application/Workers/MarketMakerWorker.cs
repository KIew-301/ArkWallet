using ArkWallet.Application.Contracts.Orchestrators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Workers;

internal class MarketMakerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketMakerWorker> _logger;

    public MarketMakerWorker(IServiceProvider serviceProvider, ILogger<MarketMakerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketMakerWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IMarketMakerOrchestrator>();

                var registerResult = await orchestrator.EnsureBotsRegisteredAsync();
                if (!registerResult.IsSuccess)
                {
                    _logger.LogError("Failed to ensure bots registered: {Error}", registerResult.Message);
                }

                var balanceResult = await orchestrator.EnsureTraderBalancesAsync();
                if (!balanceResult.IsSuccess)
                {
                    _logger.LogError("Failed to ensure trader balances: {Error}", balanceResult.Message);
                }

                var gridResult = await orchestrator.UpdateAllBotsGridAsync();
                if (!gridResult.IsSuccess)
                {
                    _logger.LogError("Failed to update bots grid: {Error}", gridResult.Message);
                }

                var processResult = await orchestrator.ProcessBotsAsync();
                if (!processResult.IsSuccess)
                {
                    _logger.LogError("Failed to process bots: {Error}", processResult.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarketMakerWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("MarketMakerWorker stopped");
    }
}