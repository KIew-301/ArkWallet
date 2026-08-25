using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result<List<AppStateData>>;

internal class AppStateQueryService(ArkWalletDbContext dbContext, ILogger<AppStateQueryService> logger) : IAppStateQueryService
{
    public async Task<Result<List<AppStateData>>> TakeAllAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var states = await dbContext.AppStates
                .AsNoTracking()
                .OrderBy(s => s.Key)
                .Select(s => new AppStateData(s.Key, s.Value))
                .ToListAsync();

            return Ok(states);
        }, logger, nameof(AppStateQueryService));
    }
}
