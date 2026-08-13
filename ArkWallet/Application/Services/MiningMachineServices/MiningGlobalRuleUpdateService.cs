using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result;

internal class MiningGlobalRuleUpdateService(ArkWalletDbContext dbContext, ILogger<MiningGlobalRuleUpdateService> logger) : IMiningGlobalRuleUpdateService
{
    public async Task<Result> UpdateRuleAsync(string symbol, decimal? currentCoefficient, decimal? futureCoefficient, decimal? baseMiningSpeed)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Fail("Требуется символ токена");

            if (currentCoefficient is null && futureCoefficient is null && baseMiningSpeed is null)
                return Fail("Не указаны параметры для обновления");

            if (currentCoefficient is null ^ futureCoefficient is null)
                return Fail("Коэффициенты задаются парой: текущий и будущий");

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningGlobalRuleAsync(symbol);

                var rule = await dbContext.MiningGlobalRules
                    .FirstOrDefaultAsync(r => r.TokenId == symbol);

                if (rule is null)
                    return Fail($"Глобальное правило для токена '{symbol}' не найдено");

                if (currentCoefficient.HasValue)
                    rule.UpdateCoefficients(currentCoefficient.Value, futureCoefficient!.Value);

                if (baseMiningSpeed.HasValue)
                    rule.UpdateBaseMiningSpeed(baseMiningSpeed.Value);

                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(MiningGlobalRuleUpdateService));
    }
}
