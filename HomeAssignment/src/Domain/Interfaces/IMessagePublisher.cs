using HomeAssignment.Domain.Events;

namespace HomeAssignment.Domain.Interfaces;

public interface IMessagePublisher
{
    Task PublishEventAsync(ActorChangedEvent actorEvent, CancellationToken cancellationToken = default);
    Task PublishCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : class;
}