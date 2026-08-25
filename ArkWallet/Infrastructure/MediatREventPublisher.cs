using ArkWallet.Domain.Common;
using MediatR;

namespace ArkWallet.Infrastructure;

internal sealed class MediatREventPublisher(IMediator mediator) : IEventPublisher
{
    public Task PublishAsync(INotification domainEvent, CancellationToken cancellationToken = default)
        => mediator.Publish(domainEvent, cancellationToken);
}
