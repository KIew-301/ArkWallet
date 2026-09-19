using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionServices;

internal class SubscriptionQueryService(ArkWalletDbContext dbContext, ILogger<SubscriptionQueryService> logger) : ISubscriptionQueryService
{
    public async Task<Result<List<SubscriptionInfo>>> GetAllAsync()
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var subscriptions = await dbContext.Subscriptions
                .Select(s => new SubscriptionInfo(
                    s.Id,
                    s.Name,
                    s.Level,
                    s.PriceRubles,
                    s.PriceWeekRubles,
                    s.PriceMonthRubles,
                    s.PriceYearRubles,
                    s.MaxOrders,
                    s.MaxMiningMachines,
                    s.DurationMinutes))
                .ToListAsync();

            return Result<List<SubscriptionInfo>>.Ok(subscriptions);
        }, logger, nameof(SubscriptionQueryService));
    }

    public async Task<SubscriptionInfo?> GetBasicAsync()
    {
        var sub = await dbContext.Subscriptions
            .Where(s => s.Level == 1)
            .FirstOrDefaultAsync();

        if (sub is null)
            return null;

        return new SubscriptionInfo(
            sub.Id,
            sub.Name,
            sub.Level,
            sub.PriceRubles,
            sub.PriceWeekRubles,
            sub.PriceMonthRubles,
            sub.PriceYearRubles,
            sub.MaxOrders,
            sub.MaxMiningMachines,
            sub.DurationMinutes);
    }

    public async Task<Result<SubscriptionInfo?>> GetByIdAsync(int id)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var sub = await dbContext.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sub is null)
                return Result<SubscriptionInfo?>.Fail("Подписка не найдена");

            return Result<SubscriptionInfo?>.Ok(new SubscriptionInfo(
                sub.Id,
                sub.Name,
                sub.Level,
                sub.PriceRubles,
                sub.PriceWeekRubles,
                sub.PriceMonthRubles,
                sub.PriceYearRubles,
                sub.MaxOrders,
                sub.MaxMiningMachines,
                sub.DurationMinutes));
        }, logger, nameof(SubscriptionQueryService));
    }
}
