using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Application.Contracts.TraderServices
{
    internal interface ITraderBalanceUpdatingService
    {
        Task<TraderBalanceUpdatingResult> AddToBalanceAsync(long traderId, decimal amount);
    }

    public record TraderBalanceUpdatingResult(
        bool IsSuccess,
        string? ErrorMessage = null
    );
}
