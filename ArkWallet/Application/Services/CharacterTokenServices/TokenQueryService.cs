using ArkWallet.Application.Contracts;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Dtos;
using Microsoft.CodeAnalysis;

namespace ArkWallet.Application.Services.CharacterTokenServices
{
    internal class TokenQueryService : ITokenQueryService
    {
        readonly IUnitOfWork _unitOfWork;

        public TokenQueryService(
            IUnitOfWork unitOfWork
            )
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<TokenInfoDto>> GetAllTokensAsync()
        {
            var tokens = await _unitOfWork.Tokens.GetAllAsync();
            return tokens.Select(TokenInfoDto.FromEntity).ToList();
        }

        public async Task<decimal> GetTokenCurrentPriceAsync(string symbol)
        {
            var token = await _unitOfWork.Tokens.GetBySymbolAsync(symbol);
            return token.CurrentPrice;
        }

        public async Task<TokenInfoDto?> GetTokenInfoAsync(string symbol)
        {
            var token = await _unitOfWork.Tokens.GetBySymbolAsync(symbol);
            return TokenInfoDto.FromEntity(token);
        }
    }
}
