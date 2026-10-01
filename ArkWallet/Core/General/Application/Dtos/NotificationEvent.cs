using ArkWallet.Infrastructure.Data;
using ArkWallet.Core.General.Domain.ValueObjects;


namespace ArkWallet.Core.General.Application.Dtos
{
    internal record NotificationEvent
    (
        long Id,
        string Message
    )
    {
        static internal List<NotificationEvent> FromOrderList<T>(List<TradeOrder> orders, List<Trader> traders, ILogger<T> logger)
        {
            try
            {
                if (orders == null || orders.Count == 0)
                    return [];

                var traderById = traders.ToDictionary(t => t.Id);

                List<NotificationEvent> list = [];

                foreach (var order in orders)
                {
                    var traderId = order.TraderId;
                    if (!traderById.TryGetValue(traderId, out var trader))
                        continue;

                    var notifyOn = trader.NotificationOn;
                    var isBuy = order.Type == OrderType.Buy;
                    var message = isBuy
                        ? $"💸 Вам продали {order.Quantity} шт. токенов {order.CharacterTokenId} по {order.AverageExecutePrice:F2}{Descriptor.CurrencySymbol}"
                        : $"💸 У вас купили {order.Quantity} шт. токенов {order.CharacterTokenId} по {order.AverageExecutePrice:F2}{Descriptor.CurrencySymbol}";

                    if (notifyOn && order.Status == OrderStatus.Filled && trader.TelegramId is { } telegramId)
                        list.Add(new(telegramId, message));
                }

                return list;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, ex, "Ошибка при формировании уведомлений о выполнении ордеров"); 
                return [];
            }
        }
    };

}
