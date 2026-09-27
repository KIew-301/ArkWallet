namespace ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices
{
    /// <summary>
    /// Накопитель коллекций команд на размещение, испускаемых ботами (одно событие = одна коллекция).
    /// Каждая коллекция размещается атомарно, коллекции между собой независимы.
    /// </summary>
    public interface IOrderCollector
    {
        /// <summary>Сохраняет коллекцию команд (непустые коллекции только).</summary>
        void Add(IReadOnlyCollection<CreateOrderCommand> collection);

        /// <summary>Возвращает и очищает все накопленные коллекции.</summary>
        IReadOnlyList<IReadOnlyCollection<CreateOrderCommand>> TakeAll();
    }
}