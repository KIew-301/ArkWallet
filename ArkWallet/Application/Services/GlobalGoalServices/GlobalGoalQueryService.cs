using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.GlobalGoalServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.GlobalGoalServices;

internal class GlobalGoalQueryService(ArkWalletDbContext dbContext, ILogger<GlobalGoalQueryService> logger) : IGlobalGoalQueryService
{
    public async Task<Result<List<GlobalGoalInfo>>> GetGoalsAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var goals = await dbContext.GlobalGoals
                .OrderBy(g => g.Id)
                .Select(g => new GlobalGoalInfo(
                    g.Id,
                    g.Name,
                    g.Description,
                    g.Target,
                    g.Actual,
                    g.Progress,
                    g.AchievedCount,
                    g.Steps
                        .OrderBy(s => s.StepNumber)
                        .Select(s => new GlobalGoalStepInfo(
                            s.StepNumber,
                            s.Target,
                            s.SymbolForReward,
                            s.AmountForReward))
                        .ToList()))
                .ToListAsync();

            return Result<List<GlobalGoalInfo>>.Ok(goals);
        }, logger, nameof(GlobalGoalQueryService));
    }
}
