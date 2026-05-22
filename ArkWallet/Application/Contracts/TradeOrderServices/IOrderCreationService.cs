using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    /// <summary>
    /// Сервис для создания новых торговых ордеров
    /// </summary>
    internal interface IOrderCreationService
    {
        /// <summary>
        /// Создает новый торговый ордер и обрабатывает его через торговый движок
        /// </summary>
        /// <param name="command">Команда создания ордера с параметрами</param>
        /// <returns>Результат создания ордера с информацией о исполнении</returns>
        /// <remarks>
        /// <para>
        /// Полный процесс создания ордера:
        /// - Валидация существования трейдера и токена
        /// - Создание доменной сущности ордера
        /// - Обработка через торговый движок для поиска совпадений
        /// - Сохранение всех изменений (ордера, сделки, балансы)
        /// </para>
        /// <para>
        /// Операция выполняется в транзакции для обеспечения целостности данных.
        /// Может привести к немедленному исполнению если найдутся matching-ордера.
        /// </para>
        /// </remarks>
        Task<OrderCreationResult> CreateOrderAsync(CreateOrderCommand command);
    }

    /// <summary>
    /// Команда создания нового торгового ордера
    /// </summary>
    /// <param name="TraderId">ID трейдера в Telegram</param>
    /// <param name="Direction">Направление сделки ("купить" или "продать")</param>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="Quantity">Количество токенов</param>
    /// <param name="Price">Цена за токен</param>
    public record CreateOrderCommand(
        long TraderId,
        string Direction,
        string Symbol,
        int Quantity,
        decimal Price
    );

    /// <summary>
    /// Результат операции создания ордера
    /// </summary>
    /// <param name="IsSuccess">True если ордер успешно создан/исполнен</param>
    /// <param name="IsFilled">True если ордер полностью исполнен немедленно</param>
    /// <param name="Order">DTO созданного ордера (только при успехе)</param>
    /// <param name="ErrorMessage">Сообщение об ошибке (только при неудаче)</param>
    internal record OrderCreationResult(
        bool IsSuccess,
        bool IsFilled,
        OrderDto? Order = null,
        string? ErrorMessage = null
    );
}