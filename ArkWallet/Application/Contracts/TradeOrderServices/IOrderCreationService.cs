using ArkWallet.Application.Common;
using ArkWallet.Application.Dtos;

namespace ArkWallet.Application.Contracts.TradeOrderServices
{
    /// <summary>
    /// Сервис для создания новых торговых ордеров
    /// </summary>
    public interface IOrderCreationService
    {
        /// <summary>
        /// Создает новый торговый ордер и обрабатывает его через торговый движок
        /// </summary>
        /// <param name="command">Команда создания ордера с параметрами</param>
        /// <returns>Результат создания ордера с информацией о исполнении</returns>
        /// <remarks>
        /// <para>
        /// Полный процесс создания ордера:
        /// - Валидация существования трейдера и токена
        /// - Создание доменной сущности ордера
        /// - Обработка через торговый движок для поиска совпадений
        /// - Сохранение всех изменений (ордера, сделки, балансы)
        /// </para>
        /// <para>
        /// Операция выполняется в транзакции для обеспечения целостности данных.
        /// Может привести к немедленному исполнению если найдутся matching-ордера.
        /// </para>
        /// </remarks>
        Task<Result<OrderCreationData>> CreateOrderAsync(CreateOrderCommand command);

        /// <summary>
        /// Создает несколько торговых ордеров и обрабатывает их через торговый движок
        /// </summary>
        /// <param name="commands">Список команд создания ордеров</param>
        /// <returns>Результат создания ордеров с информацией о исполнении каждого</returns>
        /// <remarks>
        /// <para>
        /// Особенности групповой обработки:
        /// - Ордера группируются по направлению (купить/продать) и символу токена
        /// - Каждая группа обрабатывается как единый контекст для оптимального matching
        /// - Для покупок: ордера сортируются по возрастанию цены (самые выгодные первыми)
        /// - Для продаж: ордера сортируются по убыванию цены (самые выгодные первыми)
        /// - Внутри группы проверяется достаточность средств/токенов суммарно
        /// </para>
        /// <para>
        /// Пример: при создании 3х ордеров на покупку одного токена, 
        /// средства резервируются суммарно, и ордера обрабатываются от лучшей цены к худшей
        /// </para>
        /// </remarks>
        Task<Result<List<OrderCreationData>>> CreateOrdersAsync(IEnumerable<CreateOrderCommand> commands);
    }

    /// <summary>
    /// Команда создания нового торгового ордера
    /// </summary>
    /// <param name="TraderId">ID трейдера в Telegram</param>
    /// <param name="Direction">Направление сделки ("купить" или "продать")</param>
    /// <param name="Symbol">Символ токена</param>
    /// <param name="Quantity">Количество токенов</param>
    /// <param name="Price">Цена за токен</param>
    public record CreateOrderCommand(
        long TraderId,
        string Direction,
        string Symbol,
        int Quantity,
        decimal Price
    );

    /// <summary>
    /// Результат операции создания ордера
    /// </summary>
    /// <param name="IsFilled">True если ордер полностью исполнен немедленно</param>
    /// <param name="Order">DTO созданного ордера (только при успехе)</param>
    public record OrderCreationData(
        bool IsFilled,
        OrderDto Order
    );
}