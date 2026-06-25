using ArkWallet.Application.Common;

namespace ArkWallet.Application.Contracts.TraderServices
{
    /// <summary>
    /// Сервис для управления балансом трейдера
    /// </summary>
    internal interface ITraderBalanceUpdatingService
    {
        /// <summary>
        /// Пополняет баланс трейдера на указанную сумму
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="amount">Сумма для пополнения</param>
        /// <returns>Результат операции пополнения баланса</returns>
        /// <remarks>
        /// <para>
        /// Выполняет проверки:
        /// - Сумма пополнения должна быть больше 0
        /// - Трейдер должен существовать в системе
        /// </para>
        /// <para>
        /// Операция выполняется в транзакции для обеспечения целостности данных.
        /// Использует доменный метод AddToBalance для обновления баланса.
        /// </para>
        /// </remarks>
        Task<Result> AddToBalanceAsync(long traderId, decimal amount);
    }
}