using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Application.Contracts.Orchestrators;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.Orchestrators;
using static Result;

internal class MiningMachineSlotTakingTokenOrchestrator(
    ArkWalletDbContext dbContext,
    IMiningMachineSlotTakingTokenService takingTokenService,
    IPortfolioUpdatingService portfolioUpdatingService,
    ILogger<MiningMachineSlotTakingTokenOrchestrator> logger) : IMiningMachineSlotTakingTokenOrchestrator
{
    public async Task<Result> TakeTokensFromMachineAsync(long traderId, long miningMachineSlotId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var result = await takingTokenService.TakeTokensFromMachineAsync(traderId, miningMachineSlotId);
                if (!result.IsSuccess)
                    return Fail(result.Message);
                if (!result.TryGetData(out var collection))
                    return Fail("Не удалось снять токены");

                return await AddToPortfolioAsync(traderId, collection);
            });
        }, logger, nameof(MiningMachineSlotTakingTokenOrchestrator));
    }

    public async Task<Result> TakeTokensFromMachinesAsync(long traderId)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                var result = await takingTokenService.TakeTokensFromMachinesAsync(traderId);
                if (!result.IsSuccess)
                    return Fail(result.Message);
                if (!result.TryGetData(out var collections))
                    return Fail("Не удалось снять токены");

                foreach (var collection in collections)
                {
                    var portfolioResult = await AddToPortfolioAsync(traderId, collection);
                    if (!portfolioResult.IsSuccess)
                        return portfolioResult;
                }

                return Ok();
            });
        }, logger, nameof(MiningMachineSlotTakingTokenOrchestrator));
    }

    private async Task<Result> AddToPortfolioAsync(long traderId, MiningTokenCollectionResult collection)
    {
        if (collection.TokensCollected <= 0 || string.IsNullOrEmpty(collection.Symbol))
            return Ok();

        var existing = await dbContext.PortfolioItems
            .FirstOrDefaultAsync(p => p.TraderTelegramId == traderId && p.CharacterTokenId == collection.Symbol);
        var total = (existing?.Quantity ?? 0) + collection.TokensCollected;

        var portfolioResult = await portfolioUpdatingService
            .CreateOrUpdatePortfolioAsync(traderId, collection.Symbol, total);

        return portfolioResult.IsSuccess ? Ok() : Fail(portfolioResult.Message);
    }
}
