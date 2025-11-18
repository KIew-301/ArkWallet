namespace ArkWallet.Application.Contracts.SuggestionServices
{
    public interface IQuantitySuggestionService
    {
        Task<List<QuantitySuggestionDto>> GetBuyQuantitySuggestionsAsync(long traderId, string symbol);
        Task<List<QuantitySuggestionDto>> GetSellQuantitySuggestionsAsync(long traderId, string symbol);
    }

    public record QuantitySuggestionDto(int Quantity, string Label);
}
