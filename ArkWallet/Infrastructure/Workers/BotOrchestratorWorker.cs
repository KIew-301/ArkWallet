using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Infrastructure.Workers;

/// <summary>
/// Оркестратор-планировщик ботов: по расписанию (времена следующего запуска в AppState)
/// вызывает методы IBotOrchestrator. Прогон каждого шага — бот решает сам через модификаторы.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Фоновый воркер-планировщик, координирует запуски в бесконечном цикле")]
public class BotOrchestratorWorker : BackgroundService
{
    private const string BalancesKey = "BotBalancesNextExecution";
    private const string GridsKey = "BotGridsNextExecution";
    private const string PowerKey = "BotPowerNextExecution";
    private const string MarketKey = "BotMarketNextExecution";
    private const string WallKey = "BotWallNextExecution";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotOrchestratorWorker> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="BotOrchestratorWorker"/> class.</summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">Optional time provider for deterministic testing.</param>
    public BotOrchestratorWorker(IServiceProvider serviceProvider, ILogger<BotOrchestratorWorker> logger, TimeProvider? timeProvider = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Executes the background service: ensures default bots then runs scheduled jobs in a loop.</summary>
    /// <param name="stoppingToken">Token to detect cancellation.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BotOrchestratorWorker started");

        try
        {
            await EnsureBotsAndRefreshAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure default bots at startup");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScheduledJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BotOrchestratorWorker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        _logger.LogInformation("BotOrchestratorWorker stopped");
    }

    /// <summary>
    /// Обеспечивает дефолтный состав ботов и, если состав изменился (созданы/переселены боты),
    /// немедленно обновляет сетки buyer/seller, стены и силы ботов, не дожидаясь расписания.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task EnsureBotsAndRefreshAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IBotOrchestrator>();

        var ensureResult = await orchestrator.EnsureDefaultBotsAsync(ct);
        if (!ensureResult.IsSuccess)
        {
            _logger.LogWarning("Bot composition ensure failed: {EnsureMessage}", ensureResult.Message);
            return;
        }

        if (!ensureResult.TryGetData(out var data) || !data.Changed)
            return;

        _logger.LogInformation(
            "Bot composition changed (added: {BotsAdded}, moved: {BotsMoved}, powers normalized: {BotsPowerNormalized}); refreshing grids and powers now",
            data.BotsAdded, data.BotsMoved, data.BotsPowerNormalized);

        var grids = await orchestrator.UpdateBotsGridsAsync(ct);
        if (!grids.IsSuccess)
            _logger.LogWarning("Grid refresh after ensure failed: {Message}", grids.Message);

        var wall = await orchestrator.UpdateWallBotGridsAsync(ct);
        if (!wall.IsSuccess)
            _logger.LogWarning("Wall refresh after ensure failed: {Message}", wall.Message);

        var power = await orchestrator.RebalanceAllBotsPowerAsync(ct);
        if (!power.IsSuccess)
            _logger.LogWarning("Power refresh after ensure failed: {Message}", power.Message);
    }

    /// <summary>Runs all scheduled jobs (power rebalancing, grid updates, market orders, balance updates, wall updates).</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task RunScheduledJobsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IBotOrchestrator>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Метод 5 (мощность) — отдельным джобом, редко (20–40 минут).
        await RunJobAsync(dbContext, orchestrator, PowerKey,
            () => now.AddMinutes(Random.Shared.Next(20, 41)),
            o => o.RebalanceAllBotsPowerAsync(ct),
            now, ct);

        await RunJobAsync(dbContext, orchestrator, GridsKey,
            () => now.AddMinutes(2),
            o => o.UpdateBotsGridsAsync(ct),
            now, ct);

        await RunJobAsync(dbContext, orchestrator, MarketKey,
            () => now.AddSeconds(Random.Shared.Next(40, 51)),
            o => o.ExecuteMarketOrdersAsync(ct),
            now, ct);

        await RunJobAsync(dbContext, orchestrator, BalancesKey,
            () => now.AddHours(2),
            o => o.UpdateAllBotsBalancesAsync(ct),
            now, ct);

        await RunJobAsync(dbContext, orchestrator, WallKey,
            () => now.AddMinutes(Random.Shared.Next(60, 181)),
            o => o.UpdateWallBotGridsAsync(ct),
            now, ct);

        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task RunJobAsync(
        ArkWalletDbContext dbContext,
        IBotOrchestrator orchestrator,
        string key,
        Func<DateTime> scheduleNext,
        Func<IBotOrchestrator, Task<Result>> run,
        DateTime now,
        CancellationToken ct)
    {
        var state = await dbContext.AppStates.FindAsync(new object[] { key }, ct);
        var next = state?.Value is null ? null : TryParse(state.Value);

        if (state is not null && next is not null && next.Value > now)
            return;

        var result = await run(orchestrator);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"{key}: {result.Message}");

        var nextRun = scheduleNext();
        if (state is null)
            dbContext.AppStates.Add(AppState.Create(key, nextRun));
        else
            state.Update(nextRun);
    }

    private static DateTime? TryParse(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<DateTime?>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}