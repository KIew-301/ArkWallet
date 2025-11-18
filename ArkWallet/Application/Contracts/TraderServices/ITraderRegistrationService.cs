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
        Task<RegistrationResult> RegisterTraderAsync(long telegramId, string name);
    }

    /// <summary>
    /// Результат операции регистрации трейдера
    /// </summary>
    /// <param name="IsSuccess">True если регистрация успешно выполнена</param>
    /// <param name="ErrorMessage">Сообщение об ошибке (только при неудаче)</param>
    public record RegistrationResult(bool IsSuccess, string? ErrorMessage = null);
}