namespace ArkWallet.Application.Contracts.SuggestionServices
{
    /// <summary>
    /// Сервис для генерации предложений количества токенов при создании ордеров
    /// </summary>
    public interface IQuantitySuggestionService
    {
        /// <summary>
        /// Генерирует предложения количества для покупки на основе баланса трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Список предложений количества (100%, 50%, 25%, 10%, 5% баланса)</returns>
        Task<List<QuantitySuggestionDto>> GetBuyQuantitySuggestionsAsync(long traderId, string symbol);

        /// <summary>
        /// Генерирует предложения количества для продажи на основе портфеля трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Список предложений количества (100%, 50%, 25%, 10%, 5% токенов)</returns>
        Task<List<QuantitySuggestionDto>> GetSellQuantitySuggestionsAsync(long traderId, string symbol);
    }

    /// <summary>
    /// DTO предложения количества токенов
    /// </summary>
    /// <param name="Quantity">Количество токенов</param>
    public record QuantitySuggestionDto(int Quantity);
}
