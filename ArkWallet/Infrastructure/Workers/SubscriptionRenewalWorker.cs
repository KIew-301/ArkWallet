using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Infrastructure.Workers;

/// <summary>
/// Периодически продлевает активные подписки по сохранённому платёжному методу ЮKassa
/// (рекуррентные списания без участия пользователя).
/// </summary>
internal sealed class SubscriptionRenewalWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionRenewalWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RenewalWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRenewalsAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в SubscriptionRenewalWorker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    internal async Task ProcessRenewalsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
            var payment = scope.ServiceProvider.GetRequiredService<IPaymentIntegrationService>();
            var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            var horizon = now.Add(RenewalWindow);

            var dueCandidates = await db.Traders
                .Where(t => t.SubscriptionId != null
                         && t.SubscriptionExpiresAtUtc != null
                         && t.SubscriptionExpiresAtUtc.Value > now
                         && t.SubscriptionExpiresAtUtc.Value <= horizon
                         && t.SavedPaymentMethodId != null
                         && !db.SubscriptionPayments.Any(p =>
                             p.TraderId == t.TelegramId && p.Status == "pending"))
                .ToListAsync(cancellationToken);

            if (dueCandidates.Count == 0)
                return;

            var subscriptionIds = dueCandidates.Select(t => t.SubscriptionId!.Value).Distinct().ToList();
            var subscriptions = await db.Subscriptions
                .Where(s => subscriptionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken);

            var changed = false;

            foreach (var trader in dueCandidates)
            {
                if (!subscriptions.TryGetValue(trader.SubscriptionId!.Value, out var sub))
                    continue;

                var period = await ResolvePeriodAsync(db, trader.TelegramId, sub, cancellationToken);
                var amount = GetPrice(sub, period);

                var paymentResult = await payment.CreatePaymentAsync(
                    new PaymentRequest
                    {
                        TraderTelegramId = trader.TelegramId,
                        AmountRubles = amount,
                        SubscriptionId = sub.Id,
                        Description = $"Автопродление подписки {sub.Name}",
                        Period = (int)period,
                        SavePaymentMethod = false,
                        PaymentMethodId = trader.SavedPaymentMethodId,
                    },
                    cancellationToken);

                if (!paymentResult.IsSuccess || string.IsNullOrEmpty(paymentResult.PaymentId))
                {
                    logger.LogWarning("Не удалось создать платёж автопродления для трейдера {TraderId}", trader.TelegramId);
                    continue;
                }

                db.SubscriptionPayments.Add(new SubscriptionPayment
                {
                    TraderId = trader.TelegramId,
                    SubscriptionId = sub.Id,
                    Period = (int)period,
                    AmountRubles = amount,
                    ExternalPaymentId = paymentResult.PaymentId,
                    Status = "pending",
                    PaymentMethodId = trader.SavedPaymentMethodId,
                    CreatedAtUtc = now
                });
                changed = true;
            }

            if (changed)
                await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке автопродлений подписок");
        }
    }

    private static async Task<SubscriptionPeriod> ResolvePeriodAsync(
        ArkWalletDbContext db, long traderId, Subscription sub, CancellationToken cancellationToken)
    {
        var last = await db.SubscriptionPayments
            .Where(p => p.TraderId == traderId
                     && p.SubscriptionId == sub.Id
                     && p.Status == "succeeded")
            .OrderByDescending(p => p.SucceededAtUtc)
            .Select(p => (int?)p.Period)
            .FirstOrDefaultAsync(cancellationToken);

        if (last.HasValue && Enum.IsDefined(typeof(SubscriptionPeriod), last.Value))
            return (SubscriptionPeriod)last.Value;

        return SubscriptionPeriod.Month;
    }

    private static decimal GetPrice(Subscription sub, SubscriptionPeriod period) => period switch
    {
        SubscriptionPeriod.Week => sub.PriceWeekRubles,
        SubscriptionPeriod.Month => sub.PriceMonthRubles,
        SubscriptionPeriod.Year => sub.PriceYearRubles,
        _ => sub.PriceMonthRubles
    };
}