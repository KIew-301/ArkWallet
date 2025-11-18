namespace ArkWallet.Domain.ValueObjects
{
    public enum OrderType
    {
        Buy,
        Sell
    }

    public enum OrderStatus
    {
        Active,     // В стакане
        Filled,     // Полностью исполнен
        Cancelled,  // Отменен
        Expired     // Истек
    }
}
