using ArkWallet.Application.Contracts.SuggestionServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
