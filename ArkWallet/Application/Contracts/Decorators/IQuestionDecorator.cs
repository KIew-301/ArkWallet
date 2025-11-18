using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.Decorators
{
    /// <summary>
    /// Декоратор для обогащения вопросов визарда контекстной информацией
    /// </summary>
    public interface IQuestionDecorator
    {
        /// <summary>
        /// Обогащает базовый вопрос визарда контекстной информацией о пользователе
        /// </summary>
        /// <param name="stepName">Идентификатор текущего шага визарда</param>
        /// <param name="baseQuestion">Базовый текст вопроса для декорирования</param>
        /// <param name="session">Сессия пользователя с данными и контекстом</param>
        /// <returns>Обогащенный вопрос с дополнительной контекстной информацией</returns>
        /// <remarks>
        /// <para>
        /// Добавляет релевантную финансовую информацию в зависимости от шага:
        /// - Для set_token: список доступных токенов
        /// - Для set_quantity: балансы и лимиты в зависимости от направления сделки
        /// - Для set_price: текущую рыночную цену токена
        /// </para>
        /// <para>
        /// Возвращает базовый вопрос без изменений если шаг не поддерживается.
        /// </para>
        /// </remarks>
        Task<string> DecorateQuestionAsync(string stepName, string baseQuestion, UserSession session);
    }
}