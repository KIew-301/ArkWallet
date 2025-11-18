using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TraderServices
{
    /// <summary>
    /// Сервис для запросов информации о трейдерах
    /// </summary>
    public interface ITraderQueryService
    {
        /// <summary>
        /// Получает основную информацию о трейдере
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>DTO с информацией о трейдере или null если не найден</returns>
        Task<TraderInfoDto?> GetTraderInfoAsync(long traderId);

        /// <summary>
        /// Получает общий баланс трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Общий баланс трейдера (0 если трейдер не найден)</returns>
        Task<decimal> GetTraderBalanceAsync(long traderId);

        /// <summary>
        /// Получает доступный баланс трейдера (общий баланс минус зарезервированный в ордерах)
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Доступный баланс для новых операций (0 если трейдер не найден)</returns>
        /// <remarks>
        /// Доступный баланс = общий баланс - сумма зарезервированная в активных ордерах на покупку
        /// </remarks>
        Task<decimal> GetTraderAvailableBalanceAsync(long traderId);
    }
}