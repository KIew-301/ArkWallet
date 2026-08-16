using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using TradingOrder = ArkWallet.Domain.TradingContext.Order;

namespace ArkWallet.Application.Dtos
{
    /// <summary>
    /// DTO ордера на покупку/продажу токена
    /// </summary>
    /// <param name="Id">Уникальный идентификатор ордера</param>
    /// <param name="Direction">Направление ордера (покупка или продажа)</param>
    /// <param name="OwnerId">Telegram ID владельца ордера</param>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="Quantity">Количество токенов</param>
    /// <param name="Price">Цена за единицу токена</param>
    /// <param name="Status">Статус ордера</param>
    /// <param name="CreatedAt">Дата и время создания ордера</param>
    public record OrderDto(
        string Id,
        OrderType Direction,
        long OwnerId,
        string Symbol,
        int Quantity,
        decimal Price,
        OrderStatus Status,
        DateTime CreatedAt,
        decimal AverageExecutePrice = 0m
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
                order.CreatedAt,
                order.AverageExecutePrice
                );
        }

        internal static OrderDto FromAggregate(TradingOrder order, long traderId)
        {
            if (order == null)
                throw new Exception("Невозможно создать DTO из пустого ордера");

            return new(
                order.Id,
                (OrderType)(int)order.Type,
                traderId,
                order.TokenSymbol,
                order.Quantity,
                order.Price,
                (OrderStatus)(int)order.Status,
                order.CreatedAt,
                order.AverageExecutePrice
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

    /// <summary>
    /// DTO с основной информацией о токене
    /// </summary>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="Name">Название токена</param>
    /// <param name="CurrentPrice">Текущая цена токена</param>
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



}