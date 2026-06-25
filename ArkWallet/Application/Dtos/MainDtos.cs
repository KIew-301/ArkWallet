using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Dtos
{
    public record OrderDto(
        string Id,
        OrderType Direction,
        long OwnerId,
        string Symbol,
        int Quantity,
        decimal Price,
        OrderStatus Status,
        DateTime CreatedAt
    )
    {
        internal static OrderDto FromEntity(TradeOrder order)
        {
            if (order == null)
                throw new Exception("Невозможно создать DTO из пустого ордера");

            return new(
                order.Id,
                order.Type,
                order.TraderTelegramId,
                order.CharacterTokenId,
                order.Quantity,
                order.Price,
                order.Status,
                order.CreatedAt
                );
        }

        internal string GetDesctiption()
        {
            string direction = Direction == OrderType.Buy
                ? "Купить"
                : "Продать";

            return $"[{direction} " +
                    $"токен {Symbol} " +
                    $"в количестве {Quantity} " +
                    $"по цене {Price:F2}] ";
        }
    };

    public record TokenBalanceDto(
        string Symbol,
        int Quantity
    )
    {
        internal static TokenBalanceDto? FromEntity(PortfolioItem? item, int reserved = 0)
        {
            if (item == null)
                return null;

            return new(
                item.CharacterTokenId,
                item.Quantity - reserved
                );
        }
    };

    public record TokenInfoDto(
        string Symbol,
        string Name,
        decimal CurrentPrice
    )
    {
        internal static TokenInfoDto? FromEntity(CharacterToken token)
        {
            if (token == null)
                return null;

            return new(
                token.Symbol,
                token.Name,
                token.CurrentPrice
            );
        }
    };


    public record TraderInfoDto(
        long Id,
        string Name,
        decimal Balance
    );

    public record PortfolioSummaryDto(
        List<TokenBalanceDto> Tokens
    );
}