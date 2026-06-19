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
    }
}