using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Core.SubscriptionContext.Application.Services.SubscriptionServices;

internal class SubscriptionQueryService(TimeProvider timeProvider, ArkWalletDbContext dbContext, ILogger<SubscriptionQueryService> logger) : ISubscriptionQueryService
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
                    s.DurationMinutes,
                    s.Description))
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
            sub.DurationMinutes,
            sub.Description);
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
                sub.DurationMinutes,
                sub.Description));
        }, logger, nameof(SubscriptionQueryService));
    }

    public async Task<Result<List<SubscriptionOfferInfo>>> GetOffersForTraderAsync(long traderTelegramId, CancellationToken cancellationToken = default)
    {
        return await ServiceErrorHandler.ExecuteAsync(async () =>
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;

            var activeSubscriptionId = await dbContext.Traders
                .Where(t => t.TelegramId == traderTelegramId)
                .Select(t => t.SubscriptionId)
                .FirstOrDefaultAsync(cancellationToken);

            Subscription? activeSubscription = null;
            DateTime? activeExpiresAtUtc = null;

            if (activeSubscriptionId != null)
            {
                activeSubscription = await dbContext.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == activeSubscriptionId.Value, cancellationToken);

                activeExpiresAtUtc = await dbContext.Traders
                    .Where(t => t.TelegramId == traderTelegramId)
                    .Select(t => t.SubscriptionExpiresAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var subscriptions = await dbContext.Subscriptions
                .OrderBy(s => s.Level)
                .ToListAsync(cancellationToken);

            var offers = new List<SubscriptionOfferInfo>();

            foreach (var s in subscriptions)
            {
                var offer = BuildOffer(s, activeSubscription, activeExpiresAtUtc, now);
                if (offer is not null)
                    offers.Add(offer);
            }

            return Result<List<SubscriptionOfferInfo>>.Ok(offers);
        }, logger, nameof(SubscriptionQueryService));
    }

    private static SubscriptionOfferInfo? BuildOffer(
        Subscription s,
        Subscription? activeSubscription,
        DateTime? activeExpiresAtUtc,
        DateTime now)
    {
        SubscriptionOfferAction action;
        int? bonusMinutes = null;

        if (activeSubscription is null)
        {
            action = SubscriptionOfferAction.Buy;
        }
        else if (s.Level == activeSubscription.Level)
        {
            action = SubscriptionOfferAction.Renew;
        }
        else if (s.Level > activeSubscription.Level)
        {
            action = SubscriptionOfferAction.Upgrade;
            bonusMinutes = ComputeBonusMinutes(activeSubscription, s, activeExpiresAtUtc, now);
        }
        else
        {
            return null;
        }

        return new SubscriptionOfferInfo(
            s.Id, s.Name, s.Level,
            s.PriceRubles,
            s.PriceWeekRubles, s.PriceMonthRubles, s.PriceYearRubles,
            s.MaxOrders, s.MaxMiningMachines, s.DurationMinutes,
            action, bonusMinutes, s.Description);
    }

    private static int? ComputeBonusMinutes(Subscription activeSubscription, Subscription target, DateTime? activeExpiresAtUtc, DateTime now)
    {
        if (!activeExpiresAtUtc.HasValue || target.PriceMonthRubles <= 0)
            return null;

        var remaining = activeExpiresAtUtc.Value - now;
        if (remaining <= TimeSpan.Zero)
            return null;

        var ratio = activeSubscription.PriceMonthRubles / target.PriceMonthRubles;
        return (int)Math.Floor((decimal)remaining.TotalMinutes * ratio);
    }
}
