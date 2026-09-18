namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionExpiryServices;

public interface ISubscriptionExpiryService
{
    Task<int> ProcessExpiredAsync(CancellationToken cancellationToken = default);
    Task<DateTime?> GetNextExpiryAsync(CancellationToken cancellationToken = default);
}
