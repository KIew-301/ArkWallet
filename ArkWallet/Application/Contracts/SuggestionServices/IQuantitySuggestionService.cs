namespace ArkWallet.Application.Contracts.SuggestionServices
{
    /// <summary>
    /// Сервис для генерации предложений по количеству токенов при создании ордеров
    /// </summary>
    public interface IQuantitySuggestionService
    {
        /// <summary>
        /// Генерирует список предложений по количеству для ордера на покупку
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Список предложений по количеству с метками</returns>
        /// <remarks>
        /// Предложения должны учитывать доступный баланс трейдера и текущую цену токена
        /// для реалистичных вариантов покупки.
        /// </remarks>
        Task<List<QuantitySuggestionDto>> GetBuyQuantitySuggestionsAsync(long traderId, string symbol);

        /// <summary>
        /// Генерирует список предложений по количеству для ордера на продажу
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Список предложений по количеству с метками</returns>
        /// <remarks>
        /// Предложения должны учитывать доступное количество токенов в портфеле трейдера
        /// для реалистичных вариантов продажи.
        /// </remarks>
        Task<List<QuantitySuggestionDto>> GetSellQuantitySuggestionsAsync(long traderId, string symbol);
    }

    /// <summary>
    /// DTO предложения по количеству токенов для отображения пользователю
    /// </summary>
    /// <param name="Quantity">Количество токенов</param>
    /// <param name="Label">Метка количества (например, "10%", "Половина", "Все")</param>
    public record QuantitySuggestionDto(int Quantity, string Label);
}