using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    /// <summary>
    /// Сервис для валидации торговых ордеров и их параметров
    /// </summary>
    public interface IOrderValidationService
    {
        /// <summary>
        /// Валидирует направление сделки
        /// </summary>
        /// <param name="direction">Направление сделки</param>
        /// <returns>Результат валидации</returns>
        ValidationResult ValidateDirection(string direction);

        /// <summary>
        /// Валидирует количество токенов
        /// </summary>
        /// <param name="quantity">Количество токенов</param>
        /// <returns>Результат валидации</returns>
        ValidationResult ValidateQuantity(int quantity);

        /// <summary>
        /// Валидирует возможность отмены ордера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="orderId">ID ордера</param>
        /// <returns>Результат валидации</returns>
        /// <remarks>
        /// Проверяет:
        /// - Существование ордера
        /// - Активность ордера
        /// - Права трейдера на отмену ордера
        /// </remarks>
        Task<ValidationResult> ValidateOrderCancellationAsync(long traderId, string orderId);

        /// <summary>
        /// Валидирует цену ордера
        /// </summary>
        /// <param name="price">Цена за токен</param>
        /// <returns>Результат валидации</returns>
        ValidationResult ValidatePrice(decimal price);

        /// <summary>
        /// Выполняет полную валидацию команды создания ордера
        /// </summary>
        /// <param name="request">Команда создания ордера</param>
        /// <returns>Результат валидации</returns>
        /// <remarks>
        /// Проверяет корректность введённых цены и количества.
        /// При первой ошибке валидация прекращается.
        /// </remarks>
        Task<ValidationResult> ValidateFullOrderAsync(CreateOrderCommand request);

        /// <summary>
        /// Выполняет полную валидацию группы команд создания ордеров
        /// </summary>
        /// <param name="requests">Список команд создания ордеров</param>
        /// <returns>Результат валидации</returns>
        /// <remarks>
        /// Проверяет цену и количество для каждой команды.
        /// При первой ошибке валидация прекращается.
        /// </remarks>
        Task<ValidationResult> ValidateFullOrdersAsync(IReadOnlyCollection<CreateOrderCommand> requests);
    }


}