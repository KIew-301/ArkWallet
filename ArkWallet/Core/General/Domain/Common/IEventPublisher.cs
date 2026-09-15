using MediatR;

namespace ArkWallet.Core.General.Domain.Common;

/// <summary>Публикатор доменных событий: агрегаты публикуют событие в момент факта.</summary>
internal interface IEventPublisher
{
    Task PublishAsync(INotification domainEvent, CancellationToken cancellationToken = default);
}
