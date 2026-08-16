using MediatR;

namespace ArkWallet.Domain.Common;

internal abstract class AggregateRoot
{
    private IEventPublisher? _eventPublisher;

    protected IEventPublisher EventPublisher => _eventPublisher
        ?? throw new InvalidOperationException("Event publisher is not configured");

    internal void SetEventPublisher(IEventPublisher eventPublisher) => _eventPublisher = eventPublisher;

    protected Task PublishAsync(INotification domainEvent, CancellationToken cancellationToken = default)
        => EventPublisher.PublishAsync(domainEvent, cancellationToken);
}
