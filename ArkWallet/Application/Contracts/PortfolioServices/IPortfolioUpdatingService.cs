using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.PortfolioServices
{
    /// <summary>
    /// Сервис для обновления портфеля трейдера
    /// </summary>
    public interface IPortfolioUpdatingService
    {
        /// <summary>
        /// Создает или обновляет позицию в портфеле трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="quantity">Количество токенов для добавления</param>
        /// <returns>Результат операции обновления портфеля</returns>
        /// <remarks>
        /// <para>
        /// Если позиция не существует - создает новую с текущей рыночной ценой токена.
        /// Если позиция существует - добавляет токены и пересчитывает среднюю цену покупки.
        /// </para>
        /// <para>
        /// Операция выполняется в транзакции для обеспечения целостности данных.
        /// Проверяет существование токена перед обновлением портфеля.
        /// </para>
        /// </remarks>
        Task<Result> CreateOrUpdatePortfolioAsync(long traderId, string symbol, int quantity);
    }
}