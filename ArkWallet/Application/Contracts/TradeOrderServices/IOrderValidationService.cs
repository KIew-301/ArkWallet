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
        /// Валидирует токен для сделки с учетом направления
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="direction">Направление сделки</param>
        /// <returns>Результат валидации</returns>
        /// <remarks>
        /// Для продажи проверяет что токен присутствует в портфеле трейдера.
        /// Для покупки проверка не требуется.
        /// </remarks>
        Task<ValidationResult> ValidateTokenAsync(long traderId, string symbol, string direction);

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
        /// Валидирует возможность создания ордера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="direction">Направление сделки</param>
        /// <param name="quantity">Количество токенов</param>
        /// <param name="price">Цена за токен</param>
        /// <returns>Результат валидации</returns>
        /// <remarks>
        /// <para>
        /// Для покупки проверяет достаточность средств с учетом зарезервированных в других ордерах.
        /// Для продажи проверяет достаточность токенов с учетом зарезервированных в других ордерах.
        /// </para>
        /// <para>
        /// Предполагает что базовые валидации (направление, количество, цена) уже пройдены.
        /// </para>
        /// </remarks>
        Task<ValidationResult> ValidateOrderCreationAsync(long traderId, string symbol, string direction, int quantity, decimal price);

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
        /// Последовательно проверяет: цену, количество, наличие токена у трейдера.
        /// При первой ошибке валидация прекращается.
        /// </remarks>
        Task<ValidationResult> ValidateFullOrderAsync(CreateOrderCommand request);
    }


}