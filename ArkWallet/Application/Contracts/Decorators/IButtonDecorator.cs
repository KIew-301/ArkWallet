using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.Decorators
{
    /// <summary>
    /// Декоратор для динамического формирования кнопок интерфейса на основе контекста пользователя
    /// </summary>
    public interface IButtonDecorator
    {
        /// <summary>
        /// Динамически формирует список кнопок для шага Wizard
        /// </summary>
        /// <param name="stepName">Идентификатор текущего шага Wizard</param>
        /// <param name="baseKeyword">Базовый список кнопок для декорирования</param>
        /// <param name="session">Сессия пользователя с данными и контекстом</param>
        /// <returns>Декорированный список кнопок, адаптированный под текущий контекст</returns>
        /// <remarks>
        /// <para>
        /// Анализирует текущую команду и шаг Wizard для предоставления контекстно-зависимых кнопок:
        /// - Для /placeorder: токены из портфеля и рекомендованные цены
        /// - Для /cancelorder: активные ордера пользователя
        /// </para>
        /// <para>
        /// Возвращает пустой список если контекст не распознан или данные недоступны.
        /// </para>
        /// </remarks>
        Task<List<QuickButton>> DecorateButtonsAsync(string stepName, List<QuickButton> baseKeyword, UserSession session);
    }
}
