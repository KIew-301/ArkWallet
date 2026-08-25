using MediatR;

namespace ArkWallet.Domain.Common;

/// <summary>Публикатор доменных событий: агрегаты публикуют событие в момент факта.</summary>
internal interface IEventPublisher
{
    Task PublishAsync(INotification domainEvent, CancellationToken cancellationToken = default);
}
