using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    /// <summary>
    /// Сервис для запросов информации о торговых ордерах
    /// </summary>
    public interface IOrderQueryService
    {
        /// <summary>
        /// Получает активные ордера трейдера (ожидающие исполнения)
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Список DTO активных ордеров трейдера</returns>
        /// <remarks>
        /// Возвращает только ордера в статусе Active.
        /// Возвращает пустой список если активных ордеров нет.
        /// </remarks>
        Task<List<OrderDto>> GetActiveOrdersAsync(long traderId);

        /// <summary>
        /// Получает все ордера трейдера (включая исполненные и отмененные)
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Список DTO всех ордеров трейдера</returns>
        /// <remarks>
        /// Возвращает ордера во всех статусах: Active, Filled, Cancelled.
        /// Возвращает пустой список если ордеров нет.
        /// </remarks>
        Task<List<OrderDto>> GetOrdersAsync(long traderId);

        /// <summary>
        /// Получает ордер по идентификатору
        /// </summary>
        /// <param name="orderId">ID ордера</param>
        /// <returns>DTO ордера или null если не найден</returns>
        Task<OrderDto?> GetOrderByIdAsync(string orderId);
    }
}