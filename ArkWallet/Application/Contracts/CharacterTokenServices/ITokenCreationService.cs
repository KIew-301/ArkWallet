using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.CharacterTokenServices
{
    internal interface ITokenCreationService
    {
        Task<TokenCreationResult> CreateTokenAsync(CreateTokenCommand command);
    }

    public record CreateTokenCommand(
        string Symbol,
        string Name,
        CharacterRarity Rarity,
        decimal StartPrice,
        int TotalSupply,
        bool IsActive
    )
    {
        internal CharacterToken ToEntity()
        {
            return new()
            {
                Symbol = Symbol,
                Name = Name,
                Rarity = Rarity,
                CurrentPrice = StartPrice,
                TotalSupply = TotalSupply,
                IsActive = IsActive
            };
        }
    };

    public record TokenCreationResult(
        bool IsSuccess,
        TokenInfoDto? Token = null,
        string? ErrorMessage = null
    );
}
