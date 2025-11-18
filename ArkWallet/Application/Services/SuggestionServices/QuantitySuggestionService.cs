using ArkWallet.Application.Contracts.SuggestionServices;

namespace ArkWallet.Application.Services.SuggestionServices
{
    internal class QuantitySuggestionService : IQuantitySuggestionService
    {
        public Task<List<QuantitySuggestionDto>> GetBuyQuantitySuggestionsAsync(long traderId, string symbol)
        {
            throw new NotImplementedException();
        }

        public Task<List<QuantitySuggestionDto>> GetSellQuantitySuggestionsAsync(long traderId, string symbol)
        {
            throw new NotImplementedException();
        }
    }
}
