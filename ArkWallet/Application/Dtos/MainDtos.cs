using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
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

    public record TokenBalanceDto(
        string Symbol,
        string TokenName,
        decimal Quantity,
        decimal AverageBuyPrice,
        decimal BalanceInToken,
        decimal ProfitPercent
    )
    {
        internal static TokenBalanceDto FromEntity(PortfolioItem item)
        {
            if (item == null)
                throw new Exception($"{MethodBase.GetCurrentMethod()?.Name} - item не может быть null");

            if (item.CharacterToken == null)
                throw new Exception($"{MethodBase.GetCurrentMethod()?.Name} - item.CharacterToken не может быть null");

            var balanceInToken = item.Quantity * item.CharacterToken.CurrentPrice;
            var cost = item.Quantity * item.AverageBuyPrice;
            var procentProfit = balanceInToken / cost * 100 - 100;

            return new(
                item.CharacterTokenId,
                item.CharacterToken.Name,
                item.Quantity,
                item.AverageBuyPrice,
                balanceInToken,
                procentProfit
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