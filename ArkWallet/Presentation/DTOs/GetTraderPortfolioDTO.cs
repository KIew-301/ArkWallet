using ArkWallet.Application.Dtos;

namespace ArkWallet.Presentation.DTOs
{
    public record GetPortfolioResponse(TokenBalanceDto[] Items);
}
