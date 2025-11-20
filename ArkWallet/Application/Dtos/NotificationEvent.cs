using ArkWallet.Application.Contracts.TradeOrderServices;
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
        static internal List<NotificationEvent> FromOrderList(List<TradeOrder> orders)
        {
            if (orders == null || orders.Count == 0)
                return [];

            List<NotificationEvent> list = [];

            foreach (var order in orders)
                if (order != null && order.Status == OrderStatus.Filled)
                    list.Add(new(
                        order.TraderTelegramId,
                        $"Ордер {OrderDto.FromEntity(order).GetDesctiption()} успешно исполнен")
                    );

            return list;
        }
    };
    
}
