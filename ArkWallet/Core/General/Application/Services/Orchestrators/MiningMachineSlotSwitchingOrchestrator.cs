using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.MiningContext.Application.Contracts.MiningMachineServices;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.General.Application.Services.Orchestrators;
using static Result;

internal class MiningMachineSlotSwitchingOrchestrator(
    ArkWalletDbContext dbContext,
    IMiningMachineSlotSwitchingService switchingService,
    IPortfolioUpdatingService portfolioUpdatingService,
    ILogger<MiningMachineSlotSwitchingOrchestrator> logger) : IMiningMachineSlotSwitchingOrchestrator
{
    public async Task<Result> SwitchTargetTokenAsync(long traderId, long miningMachineSlotId, string symbol)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var result = await switchingService.SwitchTargetTokenAsync(traderId, miningMachineSlotId, symbol);
                if (!result.IsSuccess)
                    return Fail(result.Message);
                if (!result.TryGetData(out var collection))
                    return Fail("Не удалось выполнить переключение");

                if (collection.TokensCollected <= 0 || string.IsNullOrEmpty(collection.Symbol))
                    return Ok();

                var portfolioResult = await portfolioUpdatingService
                    .CreateOrUpdatePortfolioAsync(traderId, collection.Symbol, collection.TokensCollected);

                return portfolioResult.IsSuccess ? Ok() : Fail(portfolioResult.Message);
            });
        }, logger, nameof(MiningMachineSlotSwitchingOrchestrator));
    }
}
