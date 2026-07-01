using ArkWallet.Application.Contracts.CharacterTokenServices;

namespace ArkWallet.Presentation.DTOs
{
    public record GetTokenListResponse(TokenInfo[] Tokens);
}
