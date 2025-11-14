namespace ArkWallet.Application.Contracts.TraderServices
{
    public interface ITraderRegistrationService
    {
        Task<RegistrationResult> RegisterTraderAsync(long telegramId, string name);
    }

    public record RegistrationResult(bool IsSuccess, string? ErrorMessage = null);
}
