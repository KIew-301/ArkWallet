using ArkWallet.Domain.Common;
using MediatR;

namespace ArkWallet.Tests.HelpTools;

internal sealed class RecordingEventPublisher : IEventPublisher
{
    public List<INotification> Events { get; } = new();

    public Task PublishAsync(INotification domainEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(domainEvent);
        return Task.CompletedTask;
    }
}
