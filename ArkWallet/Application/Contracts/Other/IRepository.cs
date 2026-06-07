using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Application.Contracts.Other
{
    internal interface IRepository<T> where T : class
    {
        /// <summary>
        /// Получает сущность по идентификатору
        /// </summary>
        /// <param name="id">Идентификатор сущности</param>
        /// <returns>Найденная сущность или null если не найдена</returns>
        Task<T?> GetByIdAsync(object id);

        /// <summary>
        /// Получает все сущности данного типа
        /// </summary>
        /// <returns>Коллекция всех сущностей</returns>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Добавляет новую сущность в репозиторий
        /// </summary>
        /// <param name="entity">Сущность для добавления</param>
        Task AddAsync(T entity);

        /// <summary>
        /// Добавляет коллекцию сущностей в репозиторий
        /// </summary>
        /// <param name="entities">Коллекция сущностей для добавления</param>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Обновляет существующую сущность
        /// </summary>
        /// <param name="entity">Сущность с обновленными данными</param>
        Task UpdateAsync(T entity);

        /// <summary>
        /// Обновляет коллекцию сущностей
        /// </summary>
        /// <param name="entities">Коллекция сущностей для обновления</param>
        Task UpdateRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Удаляет сущность из репозитория
        /// </summary>
        /// <param name="entity">Сущность для удаления</param>
        Task RemoveAsync(T entity);

        /// <summary>
        /// Удаляет коллекцию сущностей
        /// </summary>
        /// <param name="entities">Коллекция сущностей для удаления</param>
        Task RemoveRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Проверяет существование сущности с указанным идентификатором
        /// </summary>
        /// <param name="id">Идентификатор для проверки</param>
        /// <returns>True если сущность существует, иначе False</returns>
        Task<bool> ExistsAsync(object id);
    }

    internal interface ITradeOrderRepository : IRepository<TradeOrder>
    {
        /// <summary>
        /// Получает активные ордера по символу токена
        /// </summary>
        /// <param name="symbol">Символ токена (например, "ARK_001")</param>
        /// <returns>Массив активных ордеров для указанного символа</returns>
        Task<TradeOrder[]> GetActiveBySymbolAsync(string symbol);

        /// <summary>
        /// Получает все ордера трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Массив всех ордеров принадлежащих трейдеру</returns>
        Task<TradeOrder[]> GetByTraderAsync(long traderId);

        /// <summary>
        /// Получает активные ордера трейдера (ожидающие исполнения)
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Массив ордеров в статусе Active</returns>
        Task<TradeOrder[]> GetPendingByTraderAsync(long traderId);

        /// <summary>
        /// Получает ордера по комбинированным критериям фильтрации
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="type">Тип ордера (Buy/Sell)</param>
        /// <param name="status">Статус ордера</param>
        /// <returns>Массив ордеров соответствующих всем критериям</returns>
        /// <remarks>
        /// Все параметры являются обязательными и применяются в комбинации (AND)
        /// </remarks>
        Task<TradeOrder[]> GetByOptionsAsync(long traderId, string symbol, OrderType type, OrderStatus status);

        /// <summary>
        /// Отменяет активный ордер по идентификатору
        /// </summary>
        /// <param name="orderId">ID ордера для отмены</param>
        /// <returns>True если ордер существовал и был активен, иначе False</returns>
        /// <remarks>
        /// Изменяет статус ордера на Cancelled и сохраняет изменения в базе данных
        /// </remarks>
        Task<bool> CancelOrderAsync(string orderId);

        /// <summary>
        /// Получает количество зарезервированных токенов для продажи по символу
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Общее количество неисполненных токенов в активных ордерах на продажу</returns>
        /// <remarks>
        /// Суммирует разницу между Quantity и FilledQuantity для активных ордеров на продажу
        /// </remarks>
        Task<int> GetReservedQuantityAsync(long traderId, string symbol);

        /// <summary>
        /// Получает словарь зарезервированных количеств по всем символам для продажи
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Словарь где ключ - символ токена, значение - зарезервированное количество для продажи</returns>
        Task<Dictionary<string, int>> GetReservedQuantitiesAllAsync(long traderId);

        /// <summary>
        /// Получает общий зарезервированный баланс для покупок
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Сумма зарезервированных средств в активных ордерах на покупку</returns>
        /// <remarks>
        /// Рассчитывает общую сумму средств, заблокированных в активных ордерах на покупку.
        /// Формула: сумма((Quantity - FilledQuantity) * Price) по всем активным buy-ордерам
        /// </remarks>
        Task<decimal> GetReservedBalanceAsync(long traderId);
    }

    internal interface ITraderRepository : IRepository<Trader>
    {
        /// <summary>
        /// Получает трейдера по идентификатору Telegram
        /// </summary>
        /// <param name="telegramId">ID пользователя в Telegram</param>
        /// <returns>Найденный трейдер или null если не существует</returns>
        /// <remarks>
        /// Эквивалентен вызову GetByIdAsync(telegramId) для согласованности API
        /// </remarks>
        Task<Trader?> GetByTelegramIdAsync(long telegramId);

        /// <summary>
        /// Получает список трейдеров по коллекции идентификаторов
        /// </summary>
        /// <param name="telegramIds">Коллекция ID пользователей в Telegram</param>
        /// <returns>Список трейдеров с указанными идентификаторами</returns>
        Task<List<Trader>> GetByIdsAsync(IEnumerable<long> telegramIds);

        /// <summary>
        /// Проверяет существование трейдера по идентификатору Telegram
        /// </summary>
        /// <param name="telegramId">ID пользователя в Telegram</param>
        /// <returns>True если трейдер существует, иначе False</returns>
        /// <remarks>
        /// Эквивалентен вызову ExistsAsync(telegramId) для согласованности API
        /// </remarks>
        Task<bool> ExistsByTelegramIdAsync(long telegramId);

        /// <summary>
        /// Обновляет баланс трейдера
        /// </summary>
        /// <param name="telegramId">ID пользователя в Telegram</param>
        /// <param name="newBalance">Новое значение баланса</param>
        /// <remarks>
        /// Если трейдер с указанным ID не существует, операция не выполняется.
        /// Автоматически сохраняет изменения в базе данных.
        /// </remarks>
        Task UpdateBalanceAsync(long telegramId, decimal newBalance);
    }

    internal interface IPortfolioItemRepository : IRepository<PortfolioItem>
    {
        /// <summary>
        /// Получает позицию портфеля по трейдеру и символу токена
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Позиция портфеля или null если не найдена</returns>
        /// <remarks>
        /// Символ токена автоматически преобразуется в верхний регистр
        /// </remarks>
        Task<PortfolioItem?> GetByTraderAndSymbolAsync(long traderId, string symbol);

        /// <summary>
        /// Получает все позиции портфеля трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Список всех позиций в портфеле трейдера</returns>
        Task<List<PortfolioItem>> GetByTraderAsync(long traderId);

        /// <summary>
        /// Получает позиции портфеля по коллекции трейдеров и символу токена
        /// </summary>
        /// <param name="traderIds">Коллекция ID трейдеров в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Список позиций портфеля соответствующих критериям</returns>
        /// <remarks>
        /// Символ токена автоматически преобразуется в верхний регистр
        /// </remarks>
        Task<List<PortfolioItem>> GetByTradersAndSymbolAsync(IEnumerable<long> traderIds, string symbol);

        /// <summary>
        /// Рассчитывает общую стоимость портфеля трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Суммарная стоимость всех позиций в портфеле</returns>
        /// <remarks>
        /// Расчет выполняется как сумма GetTotalValue() для каждой позиции портфеля
        /// </remarks>
        Task<decimal> GetTotalPortfolioValueAsync(long traderId);

        /// <summary>
        /// Добавляет новую позицию или обновляет существующую в портфеле
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <param name="symbol">Символ токена</param>
        /// <param name="quantity">Количество токенов для добавления</param>
        /// <param name="price">Цена приобретения токенов</param>
        /// <remarks>
        /// <para>
        /// Если позиция не существует - создает новую запись.
        /// Если позиция существует - обновляет количество и пересчитывает среднюю цену покупки.
        /// </para>
        /// <para>
        /// Формула пересчета средней цены: 
        /// (старое_количество * старая_цена + новое_количество * новая_цена) / общее_количество
        /// </para>
        /// <para>
        /// Символ токена автоматически преобразуется в верхний регистр.
        /// Автоматически сохраняет изменения в базе данных.
        /// </para>
        /// </remarks>
        Task AddOrUpdateAsync(long traderId, string symbol, int quantity, decimal price);
    }

    internal interface ITradeRepository : IRepository<Trade>
    {
        /// <summary>
        /// Получает все сделки с участием трейдера
        /// </summary>
        /// <param name="traderId">ID трейдера в Telegram</param>
        /// <returns>Массив сделок где трейдер был покупателем или продавцом</returns>
        Task<Trade[]> GetByTraderAsync(long traderId);

        /// <summary>
        /// Получает все сделки по символу токена
        /// </summary>
        /// <param name="symbol">Символ токена</param>
        /// <returns>Массив сделок с указанным токеном</returns>
        Task<Trade[]> GetBySymbolAsync(string symbol);

        /// <summary>
        /// Получает последние сделки
        /// </summary>
        /// <param name="count">Количество последних сделок для возврата</param>
        /// <returns>Массив последних сделок отсортированных по дате исполнения (сначала новые)</returns>
        Task<Trade[]> GetRecentTradesAsync(int count);
    }

    internal interface ICharacterTokenRepository : IRepository<CharacterToken>
    {
        /// <summary>
        /// Получает токен персонажа по символу
        /// </summary>
        /// <param name="symbol">Символ токена (например, "ARK_001")</param>
        /// <returns>Токен персонажа или null если не найден</returns>
        /// <remarks>
        /// Эквивалентен вызову GetByIdAsync(symbol) для согласованности API.
        /// Символ автоматически преобразуется в верхний регистр.
        /// </remarks>
        Task<CharacterToken?> GetBySymbolAsync(string symbol);

        /// <summary>
        /// Получает список активных токенов персонажей
        /// </summary>
        /// <returns>Список токенов с флагом IsActive = true</returns>
        Task<List<CharacterToken>> GetActiveTokensAsync();

        /// <summary>
        /// Получает токены персонажей по редкости
        /// </summary>
        /// <param name="rarity">Редкость персонажа</param>
        /// <returns>Список токенов указанной редкости</returns>
        Task<List<CharacterToken>> GetByRarityAsync(CharacterRarity rarity);
    }
}
