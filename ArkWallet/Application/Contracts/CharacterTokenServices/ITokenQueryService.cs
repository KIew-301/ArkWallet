using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.CharacterTokenServices
{
    /// <summary>
    /// Сервис для получения информации о токенах персонажей
    /// </summary>
    public interface ITokenQueryService
    {
        /// <summary>
        /// Получает полную информацию о токене по символу
        /// </summary>
        /// <param name="symbol">Символ токена (например, "ARK_001")</param>
        /// <returns>DTO с информацией о токене или null если не найден</returns>
        Task<TokenInfoDto?> GetTokenInfoAsync(string symbol);

        /// <summary>
        /// Получает список всех токенов в системе
        /// </summary>
        /// <returns>Список DTO с информацией о всех токенах</returns>
        Task<List<TokenInfoDto>> GetAllTokensAsync();

        /// <summary>
        /// Получает текущую цену токена по символу
        /// </summary>
        /// <param name="symbol">Символ токена (например, "ARK_001")</param>
        /// <returns>Текущая цена токена</returns>
        /// <remarks>
        /// Возвращает цену даже для неактивных токенов.
        /// Выбрасывает исключение если токен не найден.
        /// </remarks>
        Task<decimal> GetTokenCurrentPriceAsync(string symbol);
    }
}