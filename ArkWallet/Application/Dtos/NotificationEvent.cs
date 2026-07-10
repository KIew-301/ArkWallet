using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Dtos
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

                var notificationOn = traders.ToDictionary(t => t.TelegramId, t => t.NotificationOn);

                List<NotificationEvent> list = [];

                foreach (var order in orders)
                {
                    var traderId = order.TraderTelegramId;
                    var notifyOn = notificationOn[traderId];
                    var message = $"💸 Ордер {OrderDto.FromEntity(order).GetDesctiption()} успешно исполнен";


                    if (notifyOn && order.Status == OrderStatus.Filled)
                        list.Add(new(traderId, message));
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
