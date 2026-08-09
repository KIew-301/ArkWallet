using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed class SaveChangesCounter : ISaveChangesInterceptor
{
    private readonly object _lock = new();
    private int _count;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int interceptResult)
    {
        Increment();
        return interceptResult;
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int interceptResult, CancellationToken cancellationToken = default)
    {
        Increment();
        return new ValueTask<int>(interceptResult);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _count = 0;
        }
    }

    private void Increment()
    {
        lock (_lock)
        {
            _count++;
        }
    }
}
