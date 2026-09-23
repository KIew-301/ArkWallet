using ArkWallet.Core.SubscriptionContext.Application.Contracts.PaymentServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionActivationServices;
using ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionPurchaseServices;
using ArkWallet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkWallet.Infrastructure.Workers;

internal sealed class PaymentConfirmationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentConfirmationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingPaymentsAsync(stoppingToken);

                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка в PaymentConfirmationWorker");
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }
    }

    internal async Task ProcessPendingPaymentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArkWalletDbContext>();
            var payment = scope.ServiceProvider.GetRequiredService<IPaymentIntegrationService>();
            var activation = scope.ServiceProvider.GetRequiredService<ISubscriptionActivationService>();
            var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var pending = await db.SubscriptionPayments
                .Where(p => p.Status == "pending")
                .OrderBy(p => p.CreatedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            var changed = false;

            foreach (var record in pending)
            {
                var status = await payment.GetPaymentStatusAsync(record.ExternalPaymentId, cancellationToken);

                if (status.IsSucceeded)
                {
                    if (await TryMarkSucceededAsync(db, activation, record, status, now, cancellationToken))
                        changed = true;
                }
                else if (status.IsCanceled)
                {
                    record.Status = "canceled";
                    record.CanceledAtUtc = now;
                    changed = true;
                }
            }

            if (changed)
                await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке подтверждений платежей");
        }
    }

    private async Task<bool> TryMarkSucceededAsync(
        ArkWalletDbContext db,
        ISubscriptionActivationService activation,
        SubscriptionPayment record,
        PaymentStatusResult status,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var effectiveStatus = status;
        var savedPaymentMethodId = effectiveStatus.SavedPaymentMethodId ?? record.PaymentMethodId;

        var period = (SubscriptionPeriod)record.Period;
        var activationResult = await activation.ActivateAsync(record.TraderId, record.SubscriptionId, period, record.AmountRubles, record.ExternalPaymentId, cancellationToken);
        if (!activationResult.Success)
        {
            logger.LogError("Не удалось активировать подписку {SubscriptionId} для трейдера {TraderId} после оплаты", record.SubscriptionId, record.TraderId);
            return false;
        }

        if (!string.IsNullOrEmpty(savedPaymentMethodId))
        {
            record.PaymentMethodId = savedPaymentMethodId;
            var trader = await db.Traders.FirstOrDefaultAsync(t => t.TelegramId == record.TraderId, cancellationToken);
            if (trader is not null)
                trader.SavedPaymentMethodId = savedPaymentMethodId;
        }

        record.Status = "succeeded";
        record.SucceededAtUtc = now;
        return true;
    }
}
