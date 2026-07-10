namespace ArkWallet.Presentation.DTOs
{
    /// <summary>
    /// Запрос на создание ордера
    /// </summary>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="Price">Цена за токен</param>
    /// <param name="Quantity">Количество токенов</param>
    /// <param name="Direction">Направление (купить/продать)</param>
    public record CreateOrderRequest(string Symbol, decimal Price, int Quantity, string Direction);
    /// <summary>
    /// Ответ на создание ордера
    /// </summary>
    /// <param name="OrderId">ID созданного ордера</param>
    /// <param name="IsFilled">Исполнен ли ордер полностью</param>
    public record CreateOrderResponse(string OrderId, bool IsFilled);
}