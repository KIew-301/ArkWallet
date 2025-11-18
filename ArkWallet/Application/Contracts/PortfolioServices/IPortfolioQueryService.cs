using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    /// <summary>
    /// Сервис для запросов к портфелю трейдера
    /// </summary>
    public interface IPortfolioQueryService
    {
        /// <summary>
        /// Получает общий баланс токена в портфеле трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>DTO с балансом токена или null если токен отсутствует в портфеле</returns>
        Task<TokenBalanceDto?> GetTokenBalanceAsync(long traderId, string symbol);

        /// <summary>
        /// Получает список всех токенов в портфеле трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Список DTO с балансами всех токенов портфеля</returns>
        /// <remarks>
        /// Возвращает пустой список если портфель трейдера пуст.
        /// </remarks>
        Task<List<TokenBalanceDto>> GetTraderTokensAsync(long traderId);

        /// <summary>
        /// Получает доступный баланс токена (общий баланс минус зарезервированный в ордерах)
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>DTO с доступным балансом токена или null если токен отсутствует в портфеле</returns>
        /// <remarks>
        /// Доступный баланс = общее количество - количество в активных ордерах на продажу
        /// </remarks>
        Task<TokenBalanceDto?> GetAvailableTokenBalanceAsync(long traderId, string symbol);

        /// <summary>
        /// Получает список всех токенов в портфеле с доступными балансами
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Список DTO с доступными балансами всех токенов портфеля</returns>
        /// <remarks>
        /// Возвращает пустой список если портфель трейдера пуст.
        /// Для каждого токена рассчитывает доступный баланс с учетом резерва в ордерах.
        /// </remarks>
        Task<List<TokenBalanceDto>> GetAvailableTraderTokensAsync(long traderId);
    }
}