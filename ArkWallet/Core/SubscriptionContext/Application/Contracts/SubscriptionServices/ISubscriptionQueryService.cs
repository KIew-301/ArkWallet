using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.SubscriptionContext.Application.Dtos;

namespace ArkWallet.Core.SubscriptionContext.Application.Contracts.SubscriptionServices;

public interface ISubscriptionQueryService
{
    Task<Result<List<SubscriptionInfo>>> GetAllAsync();
    Task<SubscriptionInfo?> GetBasicAsync();
    Task<Result<SubscriptionInfo?>> GetByIdAsync(int id);
}
