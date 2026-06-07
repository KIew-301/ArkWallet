namespace ArkWallet.Application.Contracts.Other
{
    /// <summary>
    /// Паттерн Unit of Work для управления транзакциями и доступа к репозиториям
    /// </summary>
    /// <remarks>
    /// Обеспечивает атомарность операций и согласованность данных в рамках бизнес-транзакции.
    /// Все репозитории работают в контексте одного соединения с базой данных.
    /// </remarks>
    internal interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Репозиторий для работы с трейдерами
        /// </summary>
        ITraderRepository Traders { get; }

        /// <summary>
        /// Репозиторий для работы с торговыми ордерами
        /// </summary>
        ITradeOrderRepository Orders { get; }

        /// <summary>
        /// Репозиторий для работы с портфелями активов
        /// </summary>
        IPortfolioItemRepository Portfolios { get; }

        /// <summary>
        /// Репозиторий для работы с токенами персонажей
        /// </summary>
        ICharacterTokenRepository Tokens { get; }

        /// <summary>
        /// Репозиторий для работы с завершенными сделками
        /// </summary>
        ITradeRepository Trades { get; }

        /// <summary>
        /// Выполняет операцию в транзакции с автоматическим управлением жизненным циклом
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения асинхронной операции</typeparam>
        /// <param name="action">Асинхронная операция, которая будет выполнена в транзакции</param>
        /// <returns>Результат выполнения переданной операции</returns>
        /// <exception cref="DomainException">
        /// Возникает при нарушении бизнес-правил. Транзакция откатывается, исключение пробрасывается выше
        /// </exception>
        /// <exception cref="Exception">
        /// Возникает при непредвиденных системных ошибках. Транзакция откатывается
        /// </exception>
        /// <remarks>
        /// Автоматически управляет жизненным циклом транзакции...
        /// </remarks>
        /// <example>
        /// Пример использования ExecuteInTransactionAsync для отмены ордера
        /// </example>
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);

        /// <summary>
        /// Сохраняет все ожидающие изменения в базу данных
        /// </summary>
        /// <remarks>
        /// Фиксирует изменения, сделанные через репозитории, без создания транзакции.
        /// Для атомарных операций используйте <see cref="ExecuteInTransactionAsync{T}"/>.
        /// </remarks>
        Task SaveChangesAsync();
    }
}