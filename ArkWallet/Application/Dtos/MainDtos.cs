using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using System.Diagnostics;
using System.Reflection;

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

    /// <summary>
    /// Сводка по портфелю трейдера
    /// </summary>
    public record PortfolioSummaryDto(
        List<PortfolioItemInfo> Tokens
    );
}