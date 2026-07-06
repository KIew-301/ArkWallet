namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Ответ со списком сделок
    /// </summary>
    /// <param name="Trades">Массив сделок</param>
    public record GetTradesResponse(TradeItem[] Trades);
    /// <summary>
    /// Информация о сделке
    /// </summary>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="TraderRole">Роль трейдера (Buyer/Seller)</param>
    /// <param name="ExecutionPrice">Цена исполнения</param>
    /// <param name="Quantity">Количество</param>
    /// <param name="Profit">Прибыль/убыток</param>
    /// <param name="TradeDateTime">Дата и время сделки</param>
    public record TradeItem(string Symbol, string TraderRole, decimal ExecutionPrice, decimal Quantity, decimal Profit, DateTime TradeDateTime);
}
