namespace ArkWallet.Application.Contracts.SuggestionServices
{
    /// <summary>
    /// Сервис для генерации ценовых предложений при создании ордеров
    /// </summary>
    public interface IPriceSuggestionService
    {
        /// <summary>
        /// Генерирует список ценовых предложений для ордера на покупку
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="quantity">Количество токенов для покупки</param>
        /// <returns>Список ценовых предложений с описаниями</returns>
        /// <remarks>
        /// <para>
        /// Генерирует предложения на основе:
        /// - Доступного баланса трейдера (с учетом резерва в ордерах)
        /// - Текущей рыночной цены токена
        /// - Оптимальной цены для доступного бюджета
        /// </para>
        /// <para>
        /// Фильтрует предложения по доступному бюджету и разумным пределам.
        /// Возвращает пустой список если трейдер или токен не найдены.
        /// </para>
        /// </remarks>
        Task<List<PriceSuggestionDto>> GetBuyPriceSuggestionsAsync(long traderId, string symbol, int quantity);

        /// <summary>
        /// Генерирует список ценовых предложений для ордера на продажу
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="quantity">Количество токенов для продажи</param>
        /// <returns>Список ценовых предложений с описаниями</returns>
        /// <remarks>
        /// Генерирует предложения на основе текущей рыночной цены токена
        /// с различными стратегиями продажи (быстрая, оптимальная, выгодная).
        /// </remarks>
        Task<List<PriceSuggestionDto>> GetSellPriceSuggestionsAsync(long traderId, string symbol, int quantity);
    }

    /// <summary>
    /// DTO ценового предложения для отображения пользователю
    /// </summary>
    /// <param name="Price">Цена предложения</param>
    /// <param name="Label">Краткое название стратегии цены</param>
    /// <param name="Description">Подробное описание стратегии цены</param>
    public record PriceSuggestionDto(decimal Price, string Label, string Description);
}