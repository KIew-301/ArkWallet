using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TraderServices
{
    /// <summary>
    /// Сервис для регистрации новых трейдеров в системе
    /// </summary>
    public interface ITraderRegistrationService
    {
        /// <summary>
        /// Регистрирует нового трейдера в системе
        /// </summary>
        /// <param name="telegramId">ID пользователя в Telegram</param>
        /// <param name="name">Имя пользователя</param>
        /// <returns>Результат операции регистрации</returns>
        /// <remarks>
        /// <para>
        /// Выполняет проверки перед регистрацией:
        /// - Имя не должно быть пустым или состоять из пробелов
        /// - Telegram ID должен быть положительным числом
        /// - Трейдер с таким Telegram ID не должен существовать в системе
        /// </para>
        /// <para>
        /// Операция выполняется в транзакции для обеспечения целостности данных.
        /// Использует доменный метод Create для создания сущности трейдера.
        /// </para>
        /// </remarks>
        Task<Result> RegisterTraderAsync(long telegramId, string name);
    }
}