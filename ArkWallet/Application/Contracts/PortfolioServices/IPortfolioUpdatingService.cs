namespace ArkWallet.Application.Contracts.PortfolioServices
{
    internal interface IPortfolioUpdatingService
    {
        Task<PortfolioUpdatingResult> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity);
    }

    public record PortfolioUpdatingResult(
        bool IsSuccess,
        string? ErrorMessage = null
    );
}
