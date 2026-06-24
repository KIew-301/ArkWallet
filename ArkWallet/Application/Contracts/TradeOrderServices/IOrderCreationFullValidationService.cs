using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    /// <summary>
    /// Сервис комплексной валидации команды создания ордера
    /// </summary>
    public interface IOrderCreationFullValidationService
    {
        /// <summary>
        /// Выполняет полную валидацию команды создания ордера
        /// </summary>
        /// <param name="request">Команда создания ордера</param>
        /// <returns>Результат валидации</returns>
        /// <remarks>
        /// <para>
        /// Выполняет последовательную проверку:
        /// - Валидация цены (должна быть > 0)
        /// - Валидация количества (должно быть > 0)
        /// - Проверка наличия токена у трейдера (для продажи)
        /// </para>
        /// <para>
        /// Проверки выполняются в порядке возрастания стоимости операции.
        /// При первой же ошибке валидация прекращается.
        /// </para>
        /// </remarks>
        Task<ValidationResult> ValidateAsync(CreateOrderCommand request);
    }
}