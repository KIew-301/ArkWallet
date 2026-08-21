using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MiningMachineServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Application.Services.MiningMachineServices;
using static Result;

internal class MiningGlobalRuleUpdateService(ArkWalletDbContext dbContext, ILogger<MiningGlobalRuleUpdateService> logger) : IMiningGlobalRuleUpdateService
{
    public async Task<Result> UpdateRuleAsync(string symbol, decimal? currentCoefficient, decimal? futureCoefficient, decimal? baseTokenMiningSpeed)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var validationError = ValidateRuleUpdateParameters(symbol, currentCoefficient, futureCoefficient, baseTokenMiningSpeed);
            if (validationError != null)
                return Fail(validationError);

            return await TransactionHandler.ExecuteAsync(dbContext, async () =>
            {
                await dbContext.LockMiningGlobalRuleAsync(symbol);

                var rule = await dbContext.MiningGlobalRules
                    .FirstOrDefaultAsync(r => r.TokenId == symbol);

                if (rule is null)
                    return Fail($"Глобальное правило для токена '{symbol}' не найдено");

                ApplyRuleUpdates(rule, currentCoefficient, futureCoefficient, baseTokenMiningSpeed);

                await dbContext.SaveChangesAsync();

                return Ok();
            });
        }, logger, nameof(MiningGlobalRuleUpdateService));
    }

    /// <summary>Validates rule update arguments and returns an error message, or null when they are valid.</summary>
    private static string? ValidateRuleUpdateParameters(string symbol, decimal? currentCoefficient, decimal? futureCoefficient, decimal? baseTokenMiningSpeed)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "Требуется символ токена";

        if (currentCoefficient is null && futureCoefficient is null && baseTokenMiningSpeed is null)
            return "Не указаны параметры для обновления";

        if (currentCoefficient is null ^ futureCoefficient is null)
            return "Коэффициенты задаются парой: текущий и будущий";

        return null;
    }

    /// <summary>Applies coefficient and mining speed updates to the rule for every provided value.</summary>
    private static void ApplyRuleUpdates(
        MiningGlobalRule rule,
        decimal? currentCoefficient,
        decimal? futureCoefficient,
        decimal? baseTokenMiningSpeed)
    {
        if (currentCoefficient.HasValue)
            rule.UpdateCoefficients(currentCoefficient.Value, futureCoefficient!.Value);

        if (baseTokenMiningSpeed.HasValue)
            rule.UpdateBaseTokenMiningSpeed(baseTokenMiningSpeed.Value);
    }
}
