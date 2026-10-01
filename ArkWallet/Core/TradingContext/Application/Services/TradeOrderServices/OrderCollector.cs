using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;

namespace ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;

internal sealed class OrderCollector : IOrderCollector
{
    private readonly List<IReadOnlyCollection<CreateOrderCommand>> _collections = new();
    private readonly object _gate = new();

    public void Add(IReadOnlyCollection<CreateOrderCommand> collection)
    {
        if (collection is null || collection.Count == 0)
            return;

        lock (_gate)
            _collections.Add(collection);
    }

    public IReadOnlyList<IReadOnlyCollection<CreateOrderCommand>> TakeAll()
    {
        lock (_gate)
        {
            var snapshot = _collections.ToArray();
            _collections.Clear();
            return snapshot;
        }
    }
}