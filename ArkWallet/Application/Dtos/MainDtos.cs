using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Dtos
{
    public record OrderDto(
        string Id,
        OrderType Direction,
        string Symbol,
        int Quantity,
        decimal Price,
        OrderStatus Status,
        DateTime CreatedAt
    )
    {
        internal static OrderDto? FromEntity(TradeOrder order)
        {
            if (order == null)
                return null;

            return new(
                order.Id,
                order.Type,
                order.CharacterTokenId,
                order.Quantity,
                order.Price,
                order.Status,
                order.CreatedAt
                );
        }
    };

    public record TokenBalanceDto(
        string Symbol,
        int Quantity,
        decimal CurrentPrice
    );

    public record TokenInfoDto(
        string Symbol,
        string Name,
        decimal CurrentPrice,
        decimal? PriceChange24h
    );

    public record TraderInfoDto(
        long Id,
        string Name,
        decimal Balance
    );

    public record PortfolioSummaryDto(
        decimal TotalBalance,
        List<TokenBalanceDto> Tokens
    );
}