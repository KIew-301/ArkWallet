using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Orchestrators;
using static Result;

internal class MiningMachineSlotSellingOrchestrator(
    ArkWalletDbContext dbContext,
    IMiningMachineSlotSellingService sellingService,
    IPortfolioUpdatingService portfolioUpdatingService,
    ILogger<MiningMachineSlotSellingOrchestrator> logger) : IMiningMachineSlotSellingOrchestrator
{
    public async Task<Result> SellMachineAsync(long traderId, long miningMachineSlotId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var result = await sellingService.SellMachineAsync(traderId, miningMachineSlotId);
                if (!result.IsSuccess)
                    return Fail(result.Message);
                if (!result.TryGetData(out var collection))
                    return Fail("Не удалось продать машину");

                if (collection.TokensCollected <= 0 || string.IsNullOrEmpty(collection.Symbol))
                    return Ok();

                var portfolioResult = await portfolioUpdatingService
                    .CreateOrUpdatePortfolioAsync(traderId, collection.Symbol, collection.TokensCollected);

                return portfolioResult.IsSuccess ? Ok() : Fail(portfolioResult.Message);
            });
        }, logger, nameof(MiningMachineSlotSellingOrchestrator));
    }
}
