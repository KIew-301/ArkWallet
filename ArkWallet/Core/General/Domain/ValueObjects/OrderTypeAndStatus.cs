namespace ArkWallet.Core.General.Domain.ValueObjects
{
    /// <summary>Тип ордера</summary>
    public enum OrderType
    {
        /// <summary>Покупка</summary>
        Buy,
        /// <summary>Продажа</summary>
        Sell
    }

    /// <summary>Статус ордера</summary>
    public enum OrderStatus
    {
        /// <summary>В стакане</summary>
        Active,     // В стакане
        /// <summary>Полностью исполнен</summary>
        Filled,     // Полностью исполнен
        /// <summary>Отменен</summary>
        Cancelled,  // Отменен
        /// <summary>Истек</summary>
        Expired     // Истек
    }
}
