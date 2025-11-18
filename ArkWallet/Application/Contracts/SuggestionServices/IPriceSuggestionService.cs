namespace ArkWallet.Application.Contracts.SuggestionServices
{
    public interface IPriceSuggestionService
    {
        Task<List<PriceSuggestionDto>> GetBuyPriceSuggestionsAsync(long traderId, string symbol, int quantity);
        Task<List<PriceSuggestionDto>> GetSellPriceSuggestionsAsync(long traderId, string symbol, int quantity);
    }

    public record PriceSuggestionDto(decimal Price, string Label, string Description);
}
