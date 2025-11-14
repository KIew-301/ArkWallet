using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
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
    );

    public record TokenCreationResult(
        bool IsSuccess,
        TokenInfoDto? Token = null,
        string? ErrorMessage = null
    );
}
