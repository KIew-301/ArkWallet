using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.CharacterTokenServices
{
    public interface ITokenQueryService
    {
        Task<TokenInfoDto?> GetTokenInfoAsync(string symbol);
        Task<List<TokenInfoDto>> GetAllTokensAsync();
        Task<decimal> GetTokenCurrentPriceAsync(string symbol);
    }
}
