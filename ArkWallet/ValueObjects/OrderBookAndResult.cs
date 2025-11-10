using ArkWallet.Entities;

namespace ArkWallet.ValueObjects
{
    internal class OrderBook
    {
        public List<TradeOrder> Bids { get; set; } = new(); // Заявки на покупку
        public List<TradeOrder> Asks { get; set; } = new(); // Заявки на продажу
    }

    internal class OrderResult
    {
        public bool IsFilled { get; set; }
        public bool IsFailed { get; set; }
        public string? Message { get; set; }
        public TradeOrder? Order { get; set; }
        public List<Trade> Trades { get; set; } = new();

        public static OrderResult Pending(TradeOrder order)
            => new() { IsFilled = false, Message = "Ордер успешно выставлен",Order = order };

        public static OrderResult Filled(TradeOrder order, List<Trade> trades)
            => new() { IsFilled = true, Message = "Ордер успешно исполнен", Order = order, Trades = trades };

        public static OrderResult Failed(TradeOrder order, string error)
            => new() { IsFailed = true, Message = error, Order = order };
    }
}
